import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/task_models.dart';
import '../data/tasks_controller.dart';

/// Quick add: title first, everything else optional and one tap away.
///
/// A full form would be the wrong trade on a phone — the common case is
/// capturing a thought before it evaporates, so the sheet opens with the
/// keyboard up and can be submitted without touching anything else.
Future<bool> showQuickAddSheet(
  BuildContext context, {
  DateTime? initialDate,
}) async {
  final added = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (_) => _QuickAddSheet(initialDate: initialDate),
  );
  return added ?? false;
}

class _QuickAddSheet extends ConsumerStatefulWidget {
  const _QuickAddSheet({this.initialDate});

  final DateTime? initialDate;

  @override
  ConsumerState<_QuickAddSheet> createState() => _QuickAddSheetState();
}

class _QuickAddSheetState extends ConsumerState<_QuickAddSheet> {
  final _title = TextEditingController();
  late DateTime? _date = widget.initialDate ?? today();
  TaskPriority _priority = TaskPriority.medium;
  final _tags = <String>{};
  bool _saving = false;

  @override
  void dispose() {
    _title.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final title = _title.text.trim();
    if (title.isEmpty || _saving) return;

    setState(() => _saving = true);
    try {
      await ref.read(todayProvider.notifier).quickAdd(
        title: title,
        scheduledOn: _date,
        priority: _priority.wire,
        tags: _tags.toList(),
      );
      if (mounted) {
        HapticFeedback.mediumImpact();
        Navigator.of(context).pop(true);
      }
    } on ApiException catch (e) {
      if (mounted) {
        setState(() => _saving = false);
        showAppSnack(context, e.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final tagsAsync = ref.watch(taskTagsProvider);

    return Padding(
      padding: EdgeInsets.only(
        left: Gap.lg,
        right: Gap.lg,
        top: Gap.sm,
        bottom: MediaQuery.viewInsetsOf(context).bottom + Gap.lg,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _title,
            autofocus: true,
            textCapitalization: TextCapitalization.sentences,
            textInputAction: TextInputAction.done,
            maxLines: null,
            style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w500),
            decoration: const InputDecoration(
              hintText: 'What needs doing?',
              border: InputBorder.none,
              focusedBorder: InputBorder.none,
              enabledBorder: InputBorder.none,
              filled: false,
              contentPadding: EdgeInsets.symmetric(vertical: Gap.sm),
            ),
            onChanged: (_) => setState(() {}),
            onSubmitted: (_) => _save(),
          ),
          const SizedBox(height: Gap.sm),

          // Day shortcuts, then priority — the two things most likely to
          // differ from the default.
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                _DayChip(
                  label: 'Today',
                  selected: _date != null && dateOnly(_date!) == today(),
                  onTap: () => setState(() => _date = today()),
                ),
                const SizedBox(width: Gap.sm),
                _DayChip(
                  label: 'Tomorrow',
                  selected:
                      _date != null &&
                      dateOnly(_date!) ==
                          today().add(const Duration(days: 1)),
                  onTap: () => setState(
                    () => _date = today().add(const Duration(days: 1)),
                  ),
                ),
                const SizedBox(width: Gap.sm),
                _DayChip(
                  label: 'Someday',
                  selected: _date == null,
                  onTap: () => setState(() => _date = null),
                ),
                const SizedBox(width: Gap.sm),
                _DayChip(
                  label: _customLabel(),
                  icon: Icons.calendar_today_rounded,
                  selected: _isCustom(),
                  onTap: _pickDate,
                ),
              ],
            ),
          ),
          const SizedBox(height: Gap.sm),

          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                for (final p in TaskPriority.values) ...[
                  _DayChip(
                    label: p.wire,
                    selected: _priority == p,
                    color: p.color,
                    onTap: () => setState(() => _priority = p),
                  ),
                  const SizedBox(width: Gap.sm),
                ],
              ],
            ),
          ),

          // Tags are a nice-to-have at capture time, so they render only once
          // loaded and never block the sheet.
          if (tagsAsync.value case final tags? when tags.isNotEmpty) ...[
            const SizedBox(height: Gap.sm),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  for (final t in tags) ...[
                    _DayChip(
                      label: t.name,
                      selected: _tags.contains(t.name),
                      color: t.displayColor,
                      onTap: () => setState(() {
                        if (!_tags.remove(t.name)) _tags.add(t.name);
                      }),
                    ),
                    const SizedBox(width: Gap.sm),
                  ],
                ],
              ),
            ),
          ],

          const SizedBox(height: Gap.lg),
          FilledButton(
            onPressed: _title.text.trim().isEmpty || _saving ? null : _save,
            child: _saving
                ? const SizedBox(
                    height: 20,
                    width: 20,
                    child: CircularProgressIndicator(
                      strokeWidth: 2.2,
                      color: Colors.white,
                    ),
                  )
                : const Text('Add task'),
          ),
        ],
      ),
    );
  }

  bool _isCustom() {
    final d = _date;
    if (d == null) return false;
    final day = dateOnly(d);
    return day != today() && day != today().add(const Duration(days: 1));
  }

  String _customLabel() {
    if (!_isCustom()) return 'Pick';
    final d = _date!;
    return '${d.day}/${d.month}';
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _date ?? today(),
      firstDate: DateTime(today().year - 1),
      lastDate: DateTime(today().year + 3),
    );
    if (picked != null) setState(() => _date = dateOnly(picked));
  }
}

class _DayChip extends StatelessWidget {
  const _DayChip({
    required this.label,
    required this.selected,
    required this.onTap,
    this.icon,
    this.color,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final IconData? icon;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final tint = color ?? AppColors.accent;

    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 130),
        padding: const EdgeInsets.symmetric(horizontal: Gap.md, vertical: 8),
        decoration: BoxDecoration(
          color: selected
              ? tint.withValues(alpha: 0.15)
              : scheme.surfaceContainerHighest.withValues(alpha: 0.5),
          borderRadius: BorderRadius.circular(Radii.pill),
          border: Border.all(
            color: selected ? tint : Colors.transparent,
            width: 1.4,
          ),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Icon(
                icon,
                size: 13,
                color: selected ? tint : scheme.onSurfaceVariant,
              ),
              const SizedBox(width: 5),
            ],
            Text(
              label,
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: selected ? tint : scheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
