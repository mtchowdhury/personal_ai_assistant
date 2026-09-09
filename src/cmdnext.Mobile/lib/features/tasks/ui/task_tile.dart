import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../core/theme/app_theme.dart';
import '../data/task_models.dart';

/// One task in a list.
///
/// Deliberately not a Material `CheckboxListTile`: the circular control, the
/// metadata row, and the strike-through on completion are all part of making a
/// long list scannable at a glance, which the stock tile does not give.
class TaskTile extends StatelessWidget {
  const TaskTile({
    super.key,
    required this.task,
    required this.onToggle,
    this.onTap,
    this.showDate = false,
  });

  final TaskListItem task;
  final VoidCallback onToggle;
  final VoidCallback? onTap;

  /// Overdue lists need the date; a Today list would just repeat itself.
  final bool showDate;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final done = task.isDone;
    final meta = _metaChips(context);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(Radii.card),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: Gap.lg,
          vertical: Gap.md,
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Check(done: done, color: task.statusDisplayColor, onTap: onToggle),
            const SizedBox(width: Gap.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    task.title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 15.5,
                      height: 1.3,
                      fontWeight: FontWeight.w500,
                      // Completed work stays visible but recedes, so the eye
                      // lands on what is left.
                      decoration: done ? TextDecoration.lineThrough : null,
                      decorationColor: scheme.onSurfaceVariant,
                      color: done ? scheme.onSurfaceVariant : scheme.onSurface,
                    ),
                  ),
                  if (meta.isNotEmpty) ...[
                    const SizedBox(height: Gap.xs + 2),
                    Wrap(spacing: Gap.sm, runSpacing: Gap.xs, children: meta),
                  ],
                ],
              ),
            ),
            if (task.priority.isNotable && !done) ...[
              const SizedBox(width: Gap.sm),
              _PriorityDot(priority: task.priority),
            ],
          ],
        ),
      ),
    );
  }

  /// The metadata strip: time, duration, subtask progress, tags, notes.
  /// Returned as a list so the caller can skip the row entirely when empty.
  List<Widget> _metaChips(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final muted = TextStyle(fontSize: 12, color: scheme.onSurfaceVariant);
    final chips = <Widget>[];

    if (showDate && task.scheduledOn != null) {
      final d = task.scheduledOn!;
      final days = today().difference(dateOnly(d)).inDays;
      chips.add(
        _MetaChip(
          icon: Icons.event_rounded,
          label: switch (days) {
            1 => 'Yesterday',
            > 1 && < 7 => '$days days ago',
            >= 7 => '${(days / 7).floor()}w ago',
            _ => '${d.day}/${d.month}',
          },
          color: task.isOverdue ? AppColors.red : null,
        ),
      );
    }

    if (task.timeOfDay case final t? when t.isNotEmpty) {
      chips.add(_MetaChip(icon: Icons.schedule_rounded, label: _time(t)));
    }

    if (task.approxDurationMinutes case final m? when m > 0) {
      chips.add(
        _MetaChip(
          icon: Icons.timelapse_rounded,
          label: m >= 60
              ? '${(m / 60).toStringAsFixed(m % 60 == 0 ? 0 : 1)}h'
              : '${m}m',
        ),
      );
    }

    if (task.hasSubtasks) {
      chips.add(
        _MetaChip(
          icon: Icons.checklist_rounded,
          label: '${task.subtaskDoneCount}/${task.subtaskCount}',
        ),
      );
    }

    if (task.hasNotes) {
      chips.add(const _MetaChip(icon: Icons.notes_rounded, label: ''));
    }

    // The AI writes tasks too; showing that is useful provenance.
    if (task.source != 'manual' && task.source != 'mobile') {
      chips.add(const _MetaChip(icon: Icons.auto_awesome_rounded, label: 'AI'));
    }

    for (final tag in task.tags.take(2)) {
      chips.add(_TagChip(label: tag));
    }
    if (task.tags.length > 2) {
      chips.add(Text('+${task.tags.length - 2}', style: muted));
    }

    return chips;
  }

  /// `timeOfDay` arrives as `HH:mm:ss`; only hours and minutes are useful.
  static String _time(String raw) {
    final parts = raw.split(':');
    if (parts.length < 2) return raw;
    return '${parts[0].padLeft(2, '0')}:${parts[1]}';
  }
}

/// The circular checkbox. Large enough to hit comfortably, with haptics —
/// completing a task should feel like something happened.
class _Check extends StatelessWidget {
  const _Check({required this.done, required this.color, required this.onTap});

  final bool done;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () {
        HapticFeedback.lightImpact();
        onTap();
      },
      // A transparent hit area wider than the painted circle, so the tap
      // target reaches the 44pt iOS minimum without a huge-looking control.
      behavior: HitTestBehavior.opaque,
      child: SizedBox(
        width: 34,
        height: 34,
        child: Center(
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 160),
            curve: Curves.easeOut,
            width: 22,
            height: 22,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: done ? color : Colors.transparent,
              border: Border.all(
                color: done ? color : color.withValues(alpha: 0.55),
                width: 2,
              ),
            ),
            child: done
                ? const Icon(Icons.check_rounded, size: 14, color: Colors.white)
                : null,
          ),
        ),
      ),
    );
  }
}

class _MetaChip extends StatelessWidget {
  const _MetaChip({required this.icon, required this.label, this.color});

  final IconData icon;
  final String label;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final c = color ?? Theme.of(context).colorScheme.onSurfaceVariant;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 13, color: c),
        if (label.isNotEmpty) ...[
          const SizedBox(width: 3),
          Text(
            label,
            style: TextStyle(fontSize: 12, color: c, fontWeight: FontWeight.w500),
          ),
        ],
      ],
    );
  }
}

class _TagChip extends StatelessWidget {
  const _TagChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    // Tag colors live on the tag record, not on the task, so derive a stable
    // hue from the name rather than fetching every tag to render a list.
    final color = _hue(label);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.13),
        borderRadius: BorderRadius.circular(Radii.pill),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: color,
        ),
      ),
    );
  }

  static const _palette = [
    AppColors.violet,
    AppColors.accent,
    AppColors.teal,
    AppColors.amber,
    AppColors.red,
    AppColors.green,
  ];

  static Color _hue(String s) {
    var h = 0;
    for (final c in s.codeUnits) {
      h = (h * 31 + c) & 0x7fffffff;
    }
    return _palette[h % _palette.length];
  }
}

class _PriorityDot extends StatelessWidget {
  const _PriorityDot({required this.priority});

  final TaskPriority priority;

  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.only(top: 6),
    padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
    decoration: BoxDecoration(
      color: priority.color.withValues(alpha: 0.14),
      borderRadius: BorderRadius.circular(Radii.pill),
    ),
    child: Text(
      priority == TaskPriority.urgent ? '!!' : '!',
      style: TextStyle(
        fontSize: 11,
        height: 1.2,
        fontWeight: FontWeight.w800,
        color: priority.color,
      ),
    ),
  );
}
