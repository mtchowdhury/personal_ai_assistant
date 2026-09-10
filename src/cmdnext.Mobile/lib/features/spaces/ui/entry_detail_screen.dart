import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/space_models.dart';
import '../data/spaces_controller.dart';
import 'dynamic_field_input.dart';

/// One entry, with its schema fields editable in place.
///
/// The fields shown are whatever the entry's type declares — the same schema
/// that drove the create form drives this one, so a journal entry's mood/place
/// rating gets a proper control here too, not just a raw JSON dump.
class EntryDetailScreen extends ConsumerWidget {
  const EntryDetailScreen({
    super.key,
    required this.spaceId,
    required this.entryId,
  });

  final String spaceId;
  final String entryId;

  EntryRef get _ref => (spaceId: spaceId, entryId: entryId);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(entryDetailProvider(_ref));
    final spaceAsync = ref.watch(spaceProvider(spaceId));

    return Scaffold(
      appBar: AppBar(
        actions: [
          if (async.hasValue)
            IconButton(
              tooltip: 'Delete',
              icon: const Icon(Icons.delete_outline_rounded),
              onPressed: () => _confirmDelete(context, ref),
            ),
        ],
      ),
      body: async.when(
        loading: () => const LoadingState(),
        error: (e, _) => ErrorState(
          error: e,
          onRetry: () => ref.invalidate(entryDetailProvider(_ref)),
        ),
        data: (entry) => _Body(
          spaceId: spaceId,
          entryId: entryId,
          entry: entry,
          schema: spaceAsync.value?.typeFor(entry.type),
        ),
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Delete entry?'),
        content: const Text('This cannot be undone.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            style: TextButton.styleFrom(foregroundColor: AppColors.red),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (ok != true) return;

    try {
      await ref.read(entryDetailProvider(_ref).notifier).delete();
      if (context.mounted) Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }
}

class _Body extends ConsumerStatefulWidget {
  const _Body({
    required this.spaceId,
    required this.entryId,
    required this.entry,
    required this.schema,
  });

  final String spaceId;
  final String entryId;
  final Entry entry;

  /// Null when the space's schema has since dropped this entry type, or the
  /// space itself has not loaded yet — the raw fields still render either way.
  final EntryTypeSchema? schema;

  @override
  ConsumerState<_Body> createState() => _BodyState();
}

class _BodyState extends ConsumerState<_Body> {
  late final _title = TextEditingController(text: widget.entry.title);
  late final _body = TextEditingController(text: widget.entry.body);
  late final Map<String, Object?> _fieldValues = Map.of(widget.entry.fields);

  EntryRef get _ref => (spaceId: widget.spaceId, entryId: widget.entryId);

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _save(Future<void> Function() action) async {
    try {
      await action();
    } on ApiException catch (e) {
      if (mounted) showAppSnack(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final entry = widget.entry;
    final scheme = Theme.of(context).colorScheme;
    final controller = ref.read(entryDetailProvider(_ref).notifier);

    // Fields the schema declares, in schema order — plus any leftover values
    // from a since-removed field, shown as read-only so nothing is silently
    // dropped by editing.
    final schemaFields = widget.schema?.fields ?? const <FieldSchema>[];
    final schemaNames = schemaFields.map((f) => f.name).toSet();
    final orphanFields = entry.fields.keys.where((k) => !schemaNames.contains(k));

    return ListView(
      padding: const EdgeInsets.fromLTRB(Gap.lg, Gap.md, Gap.lg, Gap.xxl),
      children: [
        TextField(
          controller: _title,
          maxLines: null,
          textCapitalization: TextCapitalization.sentences,
          style: const TextStyle(
            fontSize: 21,
            fontWeight: FontWeight.w700,
            letterSpacing: -0.3,
          ),
          decoration: const InputDecoration(
            border: InputBorder.none,
            enabledBorder: InputBorder.none,
            focusedBorder: InputBorder.none,
            filled: false,
            isDense: true,
            contentPadding: EdgeInsets.symmetric(vertical: Gap.sm),
          ),
          onTapOutside: (_) {
            FocusScope.of(context).unfocus();
            final v = _title.text.trim();
            if (v.isNotEmpty && v != entry.title) {
              _save(() => controller.save(title: v));
            }
          },
        ),

        Row(
          children: [
            if (entry.nodePath case final p? when p.isNotEmpty) ...[
              Icon(Icons.folder_outlined, size: 13, color: scheme.onSurfaceVariant),
              const SizedBox(width: 4),
              Text(p, style: TextStyle(fontSize: 12.5, color: scheme.onSurfaceVariant)),
              const SizedBox(width: Gap.md),
            ],
            if (entry.occurredOn case final d?)
              Text(
                DateFormat('EEE d MMM y').format(d),
                style: TextStyle(fontSize: 12.5, color: scheme.onSurfaceVariant),
              ),
          ],
        ),

        const SizedBox(height: Gap.lg),
        Card(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: Gap.md, vertical: Gap.sm),
            child: TextField(
              controller: _body,
              maxLines: null,
              minLines: 3,
              textCapitalization: TextCapitalization.sentences,
              style: const TextStyle(fontSize: 15, height: 1.45),
              decoration: InputDecoration(
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                filled: false,
                hintText: 'Write something…',
                hintStyle: TextStyle(color: scheme.onSurfaceVariant),
                contentPadding: const EdgeInsets.symmetric(vertical: Gap.sm),
              ),
              onTapOutside: (_) {
                FocusScope.of(context).unfocus();
                if (_body.text != entry.body) {
                  _save(() => controller.save(body: _body.text));
                }
              },
            ),
          ),
        ),

        if (schemaFields.isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(Gap.lg),
              child: Column(
                children: [
                  for (final (i, field) in schemaFields.indexed) ...[
                    if (i > 0) const SizedBox(height: Gap.md),
                    DynamicFieldInput(
                      field: field,
                      value: _fieldValues[field.name],
                      onChanged: (v) {
                        setState(() => _fieldValues[field.name] = v);
                        _save(() => controller.save(fields: _fieldValues));
                      },
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],

        if (orphanFields.isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          Text(
            'OTHER DATA',
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w800,
              letterSpacing: 0.7,
              color: scheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: Gap.sm),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(Gap.md),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  for (final key in orphanFields)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 3),
                      child: Text(
                        '$key: ${entry.fields[key]}',
                        style: TextStyle(fontSize: 13, color: scheme.onSurfaceVariant),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],

        if (entry.tags.isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          Wrap(
            spacing: Gap.sm,
            runSpacing: Gap.sm,
            children: [
              for (final tag in entry.tags)
                Chip(
                  label: Text(tag, style: const TextStyle(fontSize: 12.5)),
                  visualDensity: VisualDensity.compact,
                  backgroundColor: scheme.surfaceContainerHighest,
                  side: BorderSide.none,
                ),
            ],
          ),
        ],
      ],
    );
  }
}
