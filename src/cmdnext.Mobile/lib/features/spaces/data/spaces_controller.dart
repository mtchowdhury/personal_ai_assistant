import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'space_models.dart';
import 'spaces_repository.dart';

final spacesListProvider = FutureProvider<List<Space>>(
  (ref) => ref.watch(spacesRepositoryProvider).spaces(),
);

final spaceTemplatesProvider = FutureProvider<List<SpaceTemplate>>(
  (ref) => ref.watch(spacesRepositoryProvider).templates(),
);

/// One space's own record — separate from the list so opening a space does
/// not wait on every other space's data.
final spaceProvider = FutureProvider.autoDispose.family<Space, String>(
  (ref, spaceId) => ref.watch(spacesRepositoryProvider).space(spaceId),
);

final spaceNodesProvider = FutureProvider.autoDispose
    .family<List<SpaceNode>, String>(
      (ref, spaceId) => ref.watch(spacesRepositoryProvider).nodes(spaceId),
    );

/// The node a space's timeline is currently filtered to, keyed by space.
/// Riverpod 3 has no `FamilyNotifier` — a family `Notifier` just takes its
/// argument in the constructor, the same shape as `TaskDetailController`.
class NodeFilterController extends Notifier<String?> {
  NodeFilterController(this.spaceId);

  final String spaceId;

  @override
  String? build() => null;

  void select(String? nodeId) => state = nodeId;
}

final nodeFilterProvider = NotifierProvider.family<
  NodeFilterController,
  String?,
  String
>(NodeFilterController.new);

/// A space's timeline: recent entries, optionally filtered to one node.
final spaceTimelineProvider = FutureProvider.autoDispose
    .family<List<EntryListItem>, String>((ref, spaceId) {
      final nodeId = ref.watch(nodeFilterProvider(spaceId));
      return ref
          .watch(spacesRepositoryProvider)
          .entries(spaceId, nodeId: nodeId, includeDescendants: nodeId != null);
    });

/// Identifies one entry within its space — an entry only makes sense in that
/// context, so both ids travel together as the family argument.
typedef EntryRef = ({String spaceId, String entryId});

/// One entry, with mutation methods.
class EntryDetailController extends AsyncNotifier<Entry> {
  EntryDetailController(this._target);

  final EntryRef _target;

  String get spaceId => _target.spaceId;
  String get entryId => _target.entryId;

  @override
  Future<Entry> build() =>
      ref.watch(spacesRepositoryProvider).entry(spaceId, entryId);

  Future<void> refresh() async {
    state = AsyncData(
      await ref.read(spacesRepositoryProvider).entry(spaceId, entryId),
    );
  }

  Future<void> save({
    String? title,
    String? body,
    Map<String, dynamic>? fields,
    List<String>? tags,
    DateTime? occurredOn,
    String? status,
  }) async {
    final updated = await ref.read(spacesRepositoryProvider).updateEntry(
      spaceId,
      entryId,
      title: title,
      body: body,
      fields: fields,
      tags: tags,
      occurredOn: occurredOn,
      status: status,
    );
    state = AsyncData(updated);
    ref.invalidate(spaceTimelineProvider(spaceId));
  }

  Future<void> delete() async {
    await ref.read(spacesRepositoryProvider).deleteEntry(spaceId, entryId);
    ref.invalidate(spaceTimelineProvider(spaceId));
  }
}

final entryDetailProvider = AsyncNotifierProvider.autoDispose
    .family<EntryDetailController, Entry, EntryRef>(EntryDetailController.new);

/// Search across every space, or scoped to one.
class SpaceSearchController extends Notifier<String> {
  @override
  String build() => '';

  void set(String query) => state = query;
}

final spaceSearchQueryProvider =
    NotifierProvider<SpaceSearchController, String>(SpaceSearchController.new);

final spaceSearchResultsProvider = FutureProvider.autoDispose<
  List<SearchResult>
>((ref) async {
  final query = ref.watch(spaceSearchQueryProvider);
  if (query.trim().isEmpty) return const [];
  return ref.watch(spacesRepositoryProvider).search(query: query);
});
