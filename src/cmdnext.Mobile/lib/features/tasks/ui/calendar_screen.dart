import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/task_models.dart';
import '../data/tasks_controller.dart';
import '../data/tasks_repository.dart';
import 'quick_add_sheet.dart';
import 'task_tile.dart';

/// Month grid plus the selected day's tasks.
///
/// The web calendar renders each day's tasks inside its cell; at phone width a
/// cell is ~50pt, which fits a dot and nothing else. So the grid carries
/// density only — a dot per task, coloured by status — and the detail lives in
/// a list underneath, which is also where the day can be edited.
class CalendarScreen extends ConsumerWidget {
  const CalendarScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final month = ref.watch(calendarMonthProvider);
    final selected = ref.watch(selectedDayProvider);
    final async = ref.watch(calendarTasksProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(DateFormat('MMMM y').format(month)),
        actions: [
          IconButton(
            tooltip: 'Today',
            icon: const Icon(Icons.today_rounded),
            onPressed: () {
              ref.read(calendarMonthProvider.notifier).jumpToToday();
              ref.read(selectedDayProvider.notifier).select(today());
            },
          ),
        ],
      ),
      body: Column(
        children: [
          _MonthBar(month: month),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: Gap.sm),
            child: _Grid(
              month: month,
              selected: selected,
              tasksByDay: async.value ?? const {},
              onSelect: (d) =>
                  ref.read(selectedDayProvider.notifier).select(d),
            ),
          ),
          const Divider(height: Gap.lg),
          Expanded(
            child: async.when(
              loading: () => const LoadingState(),
              error: (e, _) => ErrorState(
                error: e,
                onRetry: () => ref.invalidate(calendarTasksProvider),
              ),
              data: (byDay) => _DayList(
                day: selected,
                tasks: byDay[selected] ?? const [],
              ),
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () async {
          final added = await showQuickAddSheet(
            context,
            initialDate: selected,
          );
          if (added) ref.invalidate(calendarTasksProvider);
        },
        tooltip: 'Add task',
        child: const Icon(Icons.add_rounded),
      ),
    );
  }
}

class _MonthBar extends ConsumerWidget {
  const _MonthBar({required this.month});

  final DateTime month;

  @override
  Widget build(BuildContext context, WidgetRef ref) => Padding(
    padding: const EdgeInsets.symmetric(horizontal: Gap.sm),
    child: Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        IconButton(
          onPressed: () => ref.read(calendarMonthProvider.notifier).previous(),
          icon: const Icon(Icons.chevron_left_rounded),
          tooltip: 'Previous month',
        ),
        Row(
          children: [
            for (final label in const ['M', 'T', 'W', 'T', 'F', 'S', 'S'])
              SizedBox(
                width: 40,
                child: Text(
                  label,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
          ],
        ),
        IconButton(
          onPressed: () => ref.read(calendarMonthProvider.notifier).next(),
          icon: const Icon(Icons.chevron_right_rounded),
          tooltip: 'Next month',
        ),
      ],
    ),
  );
}

class _Grid extends StatelessWidget {
  const _Grid({
    required this.month,
    required this.selected,
    required this.tasksByDay,
    required this.onSelect,
  });

  final DateTime month;
  final DateTime selected;
  final Map<DateTime, List<TaskListItem>> tasksByDay;
  final void Function(DateTime) onSelect;

  @override
  Widget build(BuildContext context) {
    // Weeks start Monday, matching the weekday labels above.
    final first = DateTime(month.year, month.month);
    final leading = first.weekday - DateTime.monday;
    final gridStart = first.subtract(Duration(days: leading));
    final daysInMonth = DateTime(month.year, month.month + 1, 0).day;
    final cells = ((leading + daysInMonth) / 7).ceil() * 7;

    return Column(
      children: [
        for (var week = 0; week < cells ~/ 7; week++)
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              for (var d = 0; d < 7; d++)
                () {
                  final day = dateOnly(
                    gridStart.add(Duration(days: week * 7 + d)),
                  );
                  return _DayCell(
                    day: day,
                    inMonth: day.month == month.month,
                    isToday: day == today(),
                    isSelected: day == selected,
                    tasks: tasksByDay[day] ?? const [],
                    onTap: () => onSelect(day),
                  );
                }(),
            ],
          ),
      ],
    );
  }
}

class _DayCell extends StatelessWidget {
  const _DayCell({
    required this.day,
    required this.inMonth,
    required this.isToday,
    required this.isSelected,
    required this.tasks,
    required this.onTap,
  });

  final DateTime day;
  final bool inMonth;
  final bool isToday;
  final bool isSelected;
  final List<TaskListItem> tasks;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final open = tasks.where((t) => !t.isDone).toList();
    final overdue = open.any((t) => day.isBefore(today()));

    return SizedBox(
      width: 40,
      height: 46,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              width: 28,
              height: 28,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: isSelected
                    ? AppColors.accent
                    : isToday
                    ? AppColors.accent.withValues(alpha: 0.14)
                    : Colors.transparent,
              ),
              alignment: Alignment.center,
              child: Text(
                '${day.day}',
                style: TextStyle(
                  fontSize: 13.5,
                  fontWeight: isToday || isSelected
                      ? FontWeight.w800
                      : FontWeight.w500,
                  color: isSelected
                      ? Colors.white
                      : !inMonth
                      ? scheme.onSurfaceVariant.withValues(alpha: 0.4)
                      : isToday
                      ? AppColors.accent
                      : scheme.onSurface,
                ),
              ),
            ),
            const SizedBox(height: 3),
            // Up to three dots; more than that reads as noise at this size.
            SizedBox(
              height: 5,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  for (final t in open.take(3))
                    Container(
                      width: 4,
                      height: 4,
                      margin: const EdgeInsets.symmetric(horizontal: 1),
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        color: overdue
                            ? AppColors.red
                            : t.statusDisplayColor,
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _DayList extends ConsumerWidget {
  const _DayList({required this.day, required this.tasks});

  final DateTime day;
  final List<TaskListItem> tasks;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (tasks.isEmpty) {
      return EmptyState(
        icon: Icons.event_available_rounded,
        title: 'Nothing on ${DateFormat('EEE d MMM').format(day)}',
        message: 'Tap + to schedule something for this day.',
      );
    }

    return ListView(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, 96),
      children: [
        Padding(
          padding: const EdgeInsets.only(bottom: Gap.sm, left: Gap.xs),
          child: Text(
            DateFormat('EEEE d MMMM').format(day).toUpperCase(),
            style: TextStyle(
              fontSize: 11.5,
              fontWeight: FontWeight.w800,
              letterSpacing: 0.7,
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
          ),
        ),
        Card(
          child: Column(
            children: [
              for (final (i, task) in tasks.indexed) ...[
                if (i > 0) const Divider(indent: Gap.lg + 34 + Gap.md),
                TaskTile(
                  task: task,
                  onTap: () => context.push('/tasks/${task.id}'),
                  onToggle: () async {
                    try {
                      await ref
                          .read(tasksRepositoryProvider)
                          .toggleDone(task.id);
                      ref.invalidate(calendarTasksProvider);
                      ref.invalidate(todayProvider);
                    } on ApiException catch (e) {
                      if (context.mounted) {
                        showAppSnack(context, e.message, error: true);
                      }
                    }
                  },
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}
