import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import 'space_models.dart';

/// Talks to `/spaces`.
class SpacesRepository {
  const SpacesRepository(this._api);

  final ApiClient _api;

  static String _date(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-'
      '${d.month.toString().padLeft(2, '0')}-'
      '${d.day.toString().padLeft(2, '0')}T00:00:00';

  // ---- Templates & spaces ----

  Future<List<SpaceTemplate>> templates() async =>
      (await _api.getList('spaces/templates'))
          .map(SpaceTemplate.fromJson)
          .toList(growable: false);

  Future<List<Space>> spaces({bool includeArchived = false}) async =>
      (await _api.getList(
        'spaces',
        query: {'includeArchived': includeArchived},
      )).map(Space.fromJson).toList(growable: false);

  Future<Space> space(String id) async =>
      Space.fromJson(await _api.getObject('spaces/$id'));

  Future<Space> createSpace({
    required String name,
    required String kind,
    String? description,
    String? conventions,
  }) async {
    final json = await _api.post<Map<String, dynamic>>(
      'spaces',
      body: {
        'name': name.trim(),
        'kind': kind,
        if (description != null && description.trim().isNotEmpty)
          'description': description.trim(),
        if (conventions != null && conventions.trim().isNotEmpty)
          'conventions': conventions.trim(),
      },
    );
    return Space.fromJson(json);
  }

  Future<void> deleteSpace(String id, {bool hard = false}) =>
      _api.delete<void>('spaces/$id', query: {'hard': hard});

  // ---- Nodes ----

  Future<List<SpaceNode>> nodes(String spaceId) async =>
      (await _api.getList('spaces/$spaceId/nodes'))
          .map(SpaceNode.fromJson)
          .toList(growable: false)
        ..sort((a, b) {
          final byPath = a.path.compareTo(b.path);
          return byPath != 0 ? byPath : a.sortOrder.compareTo(b.sortOrder);
        });

  Future<SpaceNode> createNode(
    String spaceId, {
    required String name,
    String? parentId,
    String? kind,
    String? summary,
  }) async {
    final json = await _api.post<Map<String, dynamic>>(
      'spaces/$spaceId/nodes',
      body: {
        'name': name.trim(),
        if (parentId != null) 'parentId': parentId,
        if (kind != null) 'kind': kind,
        if (summary != null && summary.trim().isNotEmpty)
          'summary': summary.trim(),
      },
    );
    return SpaceNode.fromJson(json);
  }

  Future<void> deleteNode(String spaceId, String nodeId) =>
      _api.delete<void>('spaces/$spaceId/nodes/$nodeId');

  // ---- Entries ----

  /// The timeline / node-filtered list. `EntryQuery.Fields` (exact-match
  /// field filters) is left out here — a v1 simplification; nothing in the
  /// mobile UI currently needs to filter by an arbitrary schema field.
  Future<List<EntryListItem>> entries(
    String spaceId, {
    String? nodeId,
    bool includeDescendants = false,
    String? type,
    List<String>? tags,
    DateTime? from,
    DateTime? to,
    String? status,
    int take = 100,
  }) async => (await _api.getList(
    'spaces/$spaceId/entries',
    query: {
      'includeDescendants': includeDescendants,
      'take': take,
      if (nodeId != null) 'nodeId': nodeId,
      if (type != null) 'type': type,
      if (tags != null && tags.isNotEmpty) 'tags': tags,
      if (from != null) 'from': _date(from),
      if (to != null) 'to': _date(to),
      if (status != null) 'status': status,
    },
  )).map(EntryListItem.fromJson).toList(growable: false);

  Future<Entry> entry(String spaceId, String entryId) async =>
      Entry.fromJson(await _api.getObject('spaces/$spaceId/entries/$entryId'));

  Future<Entry> createEntry(
    String spaceId, {
    String? nodeId,
    required String type,
    required String title,
    String body = '',
    Map<String, dynamic>? fields,
    List<String>? tags,
    DateTime? occurredOn,
    DateTime? dueOn,
    String? status,
  }) async {
    final json = await _api.post<Map<String, dynamic>>(
      'spaces/$spaceId/entries',
      body: {
        if (nodeId != null) 'nodeId': nodeId,
        'type': type,
        'title': title.trim(),
        'body': body,
        if (fields != null) 'fieldsJson': _encodeFields(fields),
        if (tags != null) 'tags': tags,
        if (occurredOn != null) 'occurredOn': _date(occurredOn),
        if (dueOn != null) 'dueOn': _date(dueOn),
        if (status != null) 'status': status,
        'source': 'mobile',
      },
    );
    return Entry.fromJson(json);
  }

  Future<Entry> updateEntry(
    String spaceId,
    String entryId, {
    String? title,
    String? body,
    Map<String, dynamic>? fields,
    List<String>? tags,
    DateTime? occurredOn,
    DateTime? dueOn,
    String? status,
  }) async {
    final json = await _api.put<Map<String, dynamic>>(
      'spaces/$spaceId/entries/$entryId',
      body: {
        if (title != null) 'title': title.trim(),
        if (body != null) 'body': body,
        if (fields != null) 'fieldsJson': _encodeFields(fields),
        if (tags != null) 'tags': tags,
        if (occurredOn != null) 'occurredOn': _date(occurredOn),
        if (dueOn != null) 'dueOn': _date(dueOn),
        if (status != null) 'status': status,
      },
    );
    return Entry.fromJson(json);
  }

  Future<void> deleteEntry(String spaceId, String entryId) =>
      _api.delete<void>('spaces/$spaceId/entries/$entryId');

  /// The API stores this in a jsonb-backed string column and must receive
  /// camelCase — the field names already are, since they come straight from
  /// the schema.
  static String _encodeFields(Map<String, dynamic> fields) => jsonEncode(fields);

  // ---- Search ----

  Future<List<SearchResult>> search({
    String? spaceId,
    String? nodeId,
    bool includeDescendants = true,
    String? query,
    String? type,
    List<String>? tags,
    DateTime? from,
    DateTime? to,
    String mode = 'hybrid',
    int limit = 20,
  }) async {
    final json = await _api.post<List<dynamic>?>(
      'spaces/search',
      body: {
        if (spaceId != null) 'spaceId': spaceId,
        if (nodeId != null) 'nodeId': nodeId,
        'includeDescendants': includeDescendants,
        if (query != null && query.trim().isNotEmpty) 'query': query.trim(),
        if (type != null) 'type': type,
        if (tags != null && tags.isNotEmpty) 'tags': tags,
        if (from != null) 'from': _date(from),
        if (to != null) 'to': _date(to),
        'mode': mode,
        'limit': limit,
      },
    );
    return (json ?? const [])
        .whereType<Map>()
        .map((e) => SearchResult.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
  }
}

final spacesRepositoryProvider = Provider<SpacesRepository>(
  (ref) => SpacesRepository(ref.watch(apiClientProvider)),
);
