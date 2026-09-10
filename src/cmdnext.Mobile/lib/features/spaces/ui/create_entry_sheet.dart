import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/space_models.dart';
import '../data/spaces_controller.dart';
import '../data/spaces_repository.dart';
import 'dynamic_field_input.dart';

/// Captures a new entry against one of the space's declared types.
///
/// This is the schema-driven form: the title and body are always present, and
/// everything below them is generated from `EntryTypeSchema.fields` — a
/// journal space gets mood/place/rating, a learning space gets term/meaning,
/// with no per-space code anywhere in the client.
Future<bool> showCreateEntrySheet(
  BuildContext context, {
  required Space space,
  String? nodeId,
}) async {
  final created = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (_) => _CreateEntrySheet(space: space, nodeId: nodeId),
  );
  return created ?? false;
}

class _CreateEntrySheet extends ConsumerStatefulWidget {
  const _CreateEntrySheet({required this.space, this.nodeId});

  final Space space;
  final String? nodeId;

  @override
  ConsumerState<_CreateEntrySheet> createState() => _CreateEntrySheetState();
}

class _CreateEntrySheetState extends ConsumerState<_CreateEntrySheet> {
  late EntryTypeSchema _type = widget.space.usableTypes.first;
  final _title = TextEditingController();
  final _body = TextEditingController();
  final Map<String, Object?> _fieldValues = {};
  bool _saving = false;

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    super.dispose();
  }

  void _selectType(EntryTypeSchema type) {
    setState(() {
      _type = type;
      _fieldValues.clear();
    });
  }

  bool get _canSave {
    if (_title.text.trim().isEmpty) return false;
    // Required schema fields must be filled before this can be saved — the
    // server would otherwise 400, which is a worse place to find out.
    for (final f in _type.fields) {
      if (f.required && (_fieldValues[f.name] == null)) return false;
    }
    return true;
  }

  Future<void> _save() async {
    if (!_canSave || _saving) return;
    setState(() => _saving = true);

    try {
      await ref.read(spacesRepositoryProvider).createEntry(
        widget.space.id,
        nodeId: widget.nodeId,
        type: _type.type,
        title: _title.text,
        body: _body.text,
        fields: _fieldValues.isEmpty ? null : _fieldValues,
      );
      ref.invalidate(spaceTimelineProvider(widget.space.id));
      if (mounted) Navigator.of(context).pop(true);
    } on ApiException catch (e) {
      if (mounted) {
        setState(() => _saving = false);
        showAppSnack(context, e.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final types = widget.space.usableTypes;

    return Padding(
      padding: EdgeInsets.only(
        left: Gap.lg,
        right: Gap.lg,
        top: Gap.sm,
        bottom: MediaQuery.viewInsetsOf(context).bottom + Gap.lg,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (types.length > 1) ...[
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    for (final t in types) ...[
                      _TypeChip(
                        label: t.label,
                        selected: t.type == _type.type,
                        onTap: () => _selectType(t),
                      ),
                      const SizedBox(width: Gap.sm),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: Gap.md),
            ],

            TextField(
              controller: _title,
              autofocus: true,
              textCapitalization: TextCapitalization.sentences,
              style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w700),
              decoration: InputDecoration(
                hintText: 'Title',
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                filled: false,
                contentPadding: EdgeInsets.zero,
              ),
              onChanged: (_) => setState(() {}),
            ),
            const SizedBox(height: Gap.sm),
            TextField(
              controller: _body,
              maxLines: null,
              minLines: 2,
              textCapitalization: TextCapitalization.sentences,
              style: const TextStyle(fontSize: 15, height: 1.4),
              decoration: const InputDecoration(
                hintText: 'Write something…',
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                filled: false,
                contentPadding: EdgeInsets.zero,
              ),
            ),

            if (_type.fields.isNotEmpty) ...[
              const SizedBox(height: Gap.lg),
              const Divider(),
              const SizedBox(height: Gap.md),
              for (final field in _type.fields) ...[
                DynamicFieldInput(
                  field: field,
                  value: _fieldValues[field.name],
                  onChanged: (v) => setState(() => _fieldValues[field.name] = v),
                ),
                const SizedBox(height: Gap.md),
              ],
            ],

            const SizedBox(height: Gap.sm),
            FilledButton(
              onPressed: _canSave && !_saving ? _save : null,
              child: _saving
                  ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(
                        strokeWidth: 2.2,
                        color: Colors.white,
                      ),
                    )
                  : const Text('Save entry'),
            ),
          ],
        ),
      ),
    );
  }
}

class _TypeChip extends StatelessWidget {
  const _TypeChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: Gap.md, vertical: 8),
        decoration: BoxDecoration(
          color: selected
              ? AppColors.accent.withValues(alpha: 0.15)
              : scheme.surfaceContainerHighest.withValues(alpha: 0.5),
          borderRadius: BorderRadius.circular(Radii.pill),
          border: Border.all(
            color: selected ? AppColors.accent : Colors.transparent,
            width: 1.4,
          ),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? AppColors.accent : scheme.onSurfaceVariant,
          ),
        ),
      ),
    );
  }
}
