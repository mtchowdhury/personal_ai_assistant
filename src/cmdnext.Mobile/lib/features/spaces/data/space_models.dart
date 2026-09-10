import 'dart:convert';

import 'package:flutter/material.dart';

import '../../tasks/data/task_models.dart' show parseApiDate, parseApiInstant;

/// The kinds of field a space schema can declare.
///
/// The web client renders everything except `select` as a plain text box; this
/// gives each type its proper control, which matters far more on a phone where
/// the right keyboard saves real effort.
enum FieldType {
  text,
  number,
  date,
  select,
  tags;

  static FieldType parse(String? v) => switch (v?.toLowerCase()) {
    'number' => FieldType.number,
    'date' => FieldType.date,
    'select' => FieldType.select,
    'tags' => FieldType.tags,
    // Unknown types degrade to text rather than breaking the form — the
    // schema is user-editable and may gain types this build never saw.
    _ => FieldType.text,
  };
}

class FieldSchema {
  const FieldSchema({
    required this.name,
    required this.type,
    required this.required,
    this.options = const [],
  });

  final String name;
  final FieldType type;
  final bool required;
  final List<String> options;

  /// Schema field names are camelCase identifiers (`initiatedBy`); the form
  /// shows them as words.
  String get label {
    final spaced = name
        .replaceAllMapped(RegExp(r'([a-z])([A-Z])'), (m) => '${m[1]} ${m[2]})')
        .replaceAll(')', '');
    return spaced.isEmpty
        ? name
        : spaced[0].toUpperCase() + spaced.substring(1);
  }

  factory FieldSchema.fromJson(Map<String, dynamic> j) => FieldSchema(
    name: (j['name'] ?? '').toString(),
    type: FieldType.parse(j['type'] as String?),
    required: j['required'] == true,
    options: ((j['options'] as List?) ?? const [])
        .map((e) => e.toString())
        .toList(growable: false),
  );
}

/// One entry type a space accepts — "Vocab / term", "Journal entry" — with the
/// fields that belong to it.
class EntryTypeSchema {
  const EntryTypeSchema({
    required this.type,
    required this.label,
    required this.fields,
  });

  final String type;
  final String label;
  final List<FieldSchema> fields;

  factory EntryTypeSchema.fromJson(Map<String, dynamic> j) => EntryTypeSchema(
    type: (j['type'] ?? '').toString(),
    label: (j['label'] ?? '').toString(),
    fields: ((j['fields'] as List?) ?? const [])
        .whereType<Map>()
        .map((e) => FieldSchema.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false),
  );
}

class Space {
  const Space({
    required this.id,
    required this.name,
    required this.slug,
    required this.kind,
    required this.status,
    required this.entryTypes,
    this.description,
    this.conventions,
    this.createdOn,
    this.updatedOn,
  });

  final String id;
  final String name;
  final String slug;
  final String kind;
  final String? description;

  /// Free-text house rules the AI is told to follow for this space.
  final String? conventions;
  final String status;
  final DateTime? createdOn;
  final DateTime? updatedOn;

  /// Parsed out of `SchemaJson`.
  final List<EntryTypeSchema> entryTypes;

  bool get isArchived => status == 'archived';

  EntryTypeSchema? typeFor(String type) {
    for (final t in entryTypes) {
      if (t.type == type) return t;
    }
    return null;
  }

  /// A space with no declared types still accepts plain notes.
  List<EntryTypeSchema> get usableTypes => entryTypes.isEmpty
      ? const [EntryTypeSchema(type: 'note', label: 'Note', fields: [])]
      : entryTypes;

  IconData get icon => switch (kind) {
    'learning' => Icons.school_outlined,
    'journal' => Icons.auto_stories_outlined,
    'people' => Icons.people_outline_rounded,
    'process' => Icons.account_tree_outlined,
    _ => Icons.layers_outlined,
  };

  /// `SchemaJson` is a JSON string column holding an array of entry types.
  /// It is written camelCase by the API, but it is user-editable and can be
  /// malformed — a bad schema must not take the whole space down.
  static List<EntryTypeSchema> parseSchema(Object? raw) {
    if (raw == null) return const [];
    final text = raw.toString().trim();
    if (text.isEmpty || text == '{}') return const [];
    try {
      final decoded = jsonDecode(text);
      if (decoded is! List) return const [];
      return decoded
          .whereType<Map>()
          .map((e) => EntryTypeSchema.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
    } catch (_) {
      return const [];
    }
  }

  factory Space.fromJson(Map<String, dynamic> j) => Space(
    id: (j['id'] ?? '').toString(),
    name: (j['name'] ?? '').toString(),
    slug: (j['slug'] ?? '').toString(),
    kind: (j['kind'] ?? 'custom').toString(),
    description: j['description'] as String?,
    conventions: j['conventions'] as String?,
    status: (j['status'] ?? 'active').toString(),
    createdOn: parseApiInstant(j['createdOn']),
    updatedOn: parseApiInstant(j['updatedOn']),
    entryTypes: parseSchema(j['schemaJson']),
  );
}

/// A node: a topic, person, or project an entry can hang off.
class SpaceNode {
  const SpaceNode({
    required this.id,
    required this.spaceId,
    required this.name,
    required this.path,
    required this.sortOrder,
    required this.entryCount,
    required this.childCount,
    this.parentId,
    this.kind,
    this.summary,
  });

  final String id;
  final String spaceId;
  final String? parentId;
  final String name;

  /// Materialised path, e.g. `Verbs/Irregular` — the depth comes from here
  /// rather than from walking parents.
  final String path;
  final int sortOrder;
  final String? kind;
  final String? summary;
  final int entryCount;
  final int childCount;

  int get depth => path.isEmpty ? 0 : path.split('/').length - 1;

  factory SpaceNode.fromJson(Map<String, dynamic> j) => SpaceNode(
    id: (j['id'] ?? '').toString(),
    spaceId: (j['spaceId'] ?? '').toString(),
    parentId: j['parentId']?.toString(),
    name: (j['name'] ?? '').toString(),
    path: (j['path'] ?? '').toString(),
    sortOrder: (j['sortOrder'] as num?)?.toInt() ?? 0,
    kind: j['kind'] as String?,
    summary: j['summary'] as String?,
    entryCount: (j['entryCount'] as num?)?.toInt() ?? 0,
    childCount: (j['childCount'] as num?)?.toInt() ?? 0,
  );
}

/// Decodes an entry's `fieldsJson` into a usable map. Like the schema, this is
/// a JSON string column and may be junk.
Map<String, dynamic> parseFields(Object? raw) {
  if (raw == null) return {};
  final text = raw.toString().trim();
  if (text.isEmpty) return {};
  try {
    final decoded = jsonDecode(text);
    return decoded is Map ? decoded.cast<String, dynamic>() : {};
  } catch (_) {
    return {};
  }
}

class EntryListItem {
  const EntryListItem({
    required this.id,
    required this.type,
    required this.title,
    required this.excerpt,
    required this.tags,
    required this.source,
    this.nodeId,
    this.nodePath,
    this.occurredOn,
    this.dueOn,
    this.status,
    this.createdOn,
  });

  final String id;
  final String? nodeId;
  final String? nodePath;
  final String type;
  final String title;
  final String excerpt;
  final List<String> tags;
  final DateTime? occurredOn;
  final DateTime? dueOn;
  final String? status;
  final String source;
  final DateTime? createdOn;

  /// What the timeline sorts and groups by: when it happened, falling back to
  /// when it was written.
  DateTime? get timelineDate => occurredOn ?? createdOn;

  factory EntryListItem.fromJson(Map<String, dynamic> j) => EntryListItem(
    id: (j['id'] ?? '').toString(),
    nodeId: j['nodeId']?.toString(),
    nodePath: j['nodePath'] as String?,
    type: (j['type'] ?? 'note').toString(),
    title: (j['title'] ?? '').toString(),
    excerpt: (j['excerpt'] ?? '').toString(),
    tags: ((j['tags'] as List?) ?? const [])
        .map((e) => e.toString())
        .toList(growable: false),
    // occurredOn/dueOn are calendar dates, like a task's scheduledOn.
    occurredOn: parseApiDate(j['occurredOn']),
    dueOn: parseApiDate(j['dueOn']),
    status: j['status'] as String?,
    source: (j['source'] ?? 'manual').toString(),
    createdOn: parseApiInstant(j['createdOn']),
  );
}

class Entry {
  const Entry({
    required this.id,
    required this.spaceId,
    required this.type,
    required this.title,
    required this.body,
    required this.fields,
    required this.tags,
    required this.source,
    required this.attachmentCount,
    this.nodeId,
    this.nodePath,
    this.occurredOn,
    this.dueOn,
    this.status,
    this.createdOn,
    this.updatedOn,
  });

  final String id;
  final String spaceId;
  final String? nodeId;
  final String? nodePath;
  final String type;
  final String title;
  final String body;

  /// The schema-driven values, decoded from `fieldsJson`.
  final Map<String, dynamic> fields;
  final List<String> tags;
  final DateTime? occurredOn;
  final DateTime? dueOn;
  final String? status;
  final String source;
  final DateTime? createdOn;
  final DateTime? updatedOn;
  final int attachmentCount;

  factory Entry.fromJson(Map<String, dynamic> j) => Entry(
    id: (j['id'] ?? '').toString(),
    spaceId: (j['spaceId'] ?? '').toString(),
    nodeId: j['nodeId']?.toString(),
    nodePath: j['nodePath'] as String?,
    type: (j['type'] ?? 'note').toString(),
    title: (j['title'] ?? '').toString(),
    body: (j['body'] ?? '').toString(),
    fields: parseFields(j['fieldsJson']),
    tags: ((j['tags'] as List?) ?? const [])
        .map((e) => e.toString())
        .toList(growable: false),
    occurredOn: parseApiDate(j['occurredOn']),
    dueOn: parseApiDate(j['dueOn']),
    status: j['status'] as String?,
    source: (j['source'] ?? 'manual').toString(),
    createdOn: parseApiInstant(j['createdOn']),
    updatedOn: parseApiInstant(j['updatedOn']),
    attachmentCount: (j['attachmentCount'] as num?)?.toInt() ?? 0,
  );
}

/// A hit from `/spaces/search`, which blends keyword and vector matching.
class SearchResult {
  const SearchResult({
    required this.entryId,
    required this.spaceId,
    required this.spaceName,
    required this.type,
    required this.title,
    required this.snippet,
    required this.rank,
    this.nodeId,
    this.nodePath,
    this.occurredOn,
  });

  final String entryId;
  final String spaceId;
  final String spaceName;
  final String? nodeId;
  final String? nodePath;
  final String type;
  final String title;
  final String snippet;
  final DateTime? occurredOn;
  final double rank;

  factory SearchResult.fromJson(Map<String, dynamic> j) => SearchResult(
    entryId: (j['entryId'] ?? '').toString(),
    spaceId: (j['spaceId'] ?? '').toString(),
    spaceName: (j['spaceName'] ?? '').toString(),
    nodeId: j['nodeId']?.toString(),
    nodePath: j['nodePath'] as String?,
    type: (j['type'] ?? 'note').toString(),
    title: (j['title'] ?? '').toString(),
    snippet: (j['snippet'] ?? '').toString(),
    occurredOn: parseApiDate(j['occurredOn']),
    rank: (j['rank'] as num?)?.toDouble() ?? 0,
  );
}

class SpaceTemplate {
  const SpaceTemplate({
    required this.kind,
    required this.label,
    required this.description,
    required this.entryTypes,
    this.suggestedNodeKind,
  });

  final String kind;
  final String label;
  final String description;
  final String? suggestedNodeKind;
  final List<EntryTypeSchema> entryTypes;

  IconData get icon => switch (kind) {
    'learning' => Icons.school_outlined,
    'journal' => Icons.auto_stories_outlined,
    'people' => Icons.people_outline_rounded,
    'process' => Icons.account_tree_outlined,
    _ => Icons.layers_outlined,
  };

  factory SpaceTemplate.fromJson(Map<String, dynamic> j) => SpaceTemplate(
    kind: (j['kind'] ?? '').toString(),
    label: (j['label'] ?? '').toString(),
    description: (j['description'] ?? '').toString(),
    suggestedNodeKind: j['suggestedNodeKind'] as String?,
    entryTypes: ((j['entryTypes'] as List?) ?? const [])
        .whereType<Map>()
        .map((e) => EntryTypeSchema.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false),
  );
}
