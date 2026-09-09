import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/task_models.dart';
import '../data/tasks_controller.dart';
import 'quick_add_sheet.dart';
import 'task_tile.dart';

/// The Today screen.
///
/// The web client leads with a board and a table; neither survives a phone
/// screen. What matters on mobile is: what is late, what is due today, and how
/// close the day is to finished — so the screen is built around those three and
/// pushes everything else behind a filter.
class TasksScreen extends ConsumerWidget {
  const TasksScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(todayProvider);

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () => ref.read(todayProvider.notifier).refresh(),
        child: async.when(
          loading: () => const LoadingState(),
          error: (e, _) => CustomScrollView(
            // Must stay scrollable, or pull-to-refresh cannot rescue an
            // error caused by the server being briefly unreachable.
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              SliverFillRemaining(
                hasScrollBody: false,
                child: ErrorState(
                  error: e,
                  onRetry: () => ref.invalidate(todayProvider),
                ),
              ),
            ],
          ),
          data: (d) => _TodayBody(dashboard: d),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => showQuickAddSheet(context),
        tooltip: 'Add task',
        child: const Icon(Icons.add_rounded),
      ),
    );
  }
}

class _TodayBody extends ConsumerWidget {
  const _TodayBody({required this.dashboard});

  final TaskDashboard dashboard;

  Future<void> _toggle(BuildContext context, WidgetRef ref, String id) async {
    try {
      await ref.read(todayProvider.notifier).toggle(id);
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final d = dashboard;

    return CustomScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      slivers: [
        SliverAppBar.large(
          title: const Text('Today'),
          actions: [
            IconButton(
              onPressed: () => context.push('/tasks/calendar'),
              icon: const Icon(Icons.calendar_month_rounded),
              tooltip: 'Calendar',
            ),
            IconButton(
              onPressed: () => context.push('/tasks/all'),
              icon: const Icon(Icons.list_rounded),
              tooltip: 'All tasks',
            ),
          ],
        ),

        SliverToBoxAdapter(child: _DayHeader(dashboard: d)),

        if (d.todayTasks.isNotEmpty)
          _Section(
            title: 'Today',
            count: d.todayCount,
            accent: AppColors.accent,
            tasks: d.todayTasks,
            onToggle: (id) => _toggle(context, ref, id),
          ),

        if (d.overdueTasks.isNotEmpty)
          _Section(
            title: 'Overdue',
            count: d.overdueCount,
            accent: AppColors.red,
            tasks: d.overdueTasks,
            showDate: true,
            onToggle: (id) => _toggle(context, ref, id),
          ),

        if (d.todayTasks.isEmpty && d.overdueTasks.isEmpty)
          SliverFillRemaining(
            hasScrollBody: false,
            child: EmptyState(
              icon: Icons.wb_sunny_outlined,
              title: 'Nothing scheduled',
              message: d.unscheduledCount > 0
                  ? '${d.unscheduledCount} unscheduled '
                        '${d.unscheduledCount == 1 ? "task is" : "tasks are"} '
                        'waiting whenever you want them.'
                  : 'Add something and it will show up here.',
              action: FilledButton.icon(
                onPressed: () => showQuickAddSheet(context),
                icon: const Icon(Icons.add_rounded, size: 20),
                label: const Text('Add a task'),
              ),
            ),
          ),

        // Clears the FAB so the last row is never trapped underneath it.
        const SliverToBoxAdapter(child: SizedBox(height: 96)),
      ],
    );
  }
}

/// The date, a progress ring, and the counts that are not on screen as rows.
class _DayHeader extends StatelessWidget {
  const _DayHeader({required this.dashboard});

  final TaskDashboard dashboard;

  @override
  Widget build(BuildContext context) {
    final d = dashboard;
    final scheme = Theme.of(context).colorScheme;
    final now = DateTime.now();

    return Padding(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.lg),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(Gap.lg),
          child: Row(
            children: [
              _ProgressRing(
                progress: d.todayProgress,
                label: d.todayCount == 0
                    ? '—'
                    : '${d.todayDoneCount}/${d.todayCount}',
              ),
              const SizedBox(width: Gap.lg),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      DateFormat('EEEE').format(now),
                      style: const TextStyle(
                        fontSize: 17,
                        fontWeight: FontWeight.w700,
                        letterSpacing: -0.3,
                      ),
                    ),
                    Text(
                      DateFormat('d MMMM').format(now),
                      style: TextStyle(
                        fontSize: 13,
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                    const SizedBox(height: Gap.sm),
                    Text(
                      _summary(d),
                      style: TextStyle(
                        fontSize: 13,
                        height: 1.35,
                        color: d.overdueCount > 0
                            ? AppColors.red
                            : scheme.onSurfaceVariant,
                        fontWeight: d.overdueCount > 0
                            ? FontWeight.w600
                            : FontWeight.w400,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// One honest sentence about the day, rather than a wall of counters.
  static String _summary(TaskDashboard d) {
    if (d.overdueCount > 0) {
      final n = d.overdueCount;
      return '$n ${n == 1 ? 'task is' : 'tasks are'} overdue';
    }
    if (d.todayCount == 0) {
      return d.upcomingCount > 0
          ? '${d.upcomingCount} coming up later'
          : 'Nothing on the books';
    }
    final left = d.todayCount - d.todayDoneCount;
    if (left == 0) return 'All done for today';
    return '$left to go';
  }
}

class _ProgressRing extends StatelessWidget {
  const _ProgressRing({required this.progress, required this.label});

  final double progress;
  final String label;

  @override
  Widget build(BuildContext context) {
    final complete = progress >= 1;
    return SizedBox(
      width: 62,
      height: 62,
      child: Stack(
        alignment: Alignment.center,
        children: [
          SizedBox.expand(
            child: TweenAnimationBuilder<double>(
              tween: Tween(begin: 0, end: progress),
              duration: const Duration(milliseconds: 500),
              curve: Curves.easeOutCubic,
              builder: (context, value, _) => CircularProgressIndicator(
                value: value,
                strokeWidth: 5.5,
                strokeCap: StrokeCap.round,
                backgroundColor: Theme.of(
                  context,
                ).colorScheme.surfaceContainerHighest,
                valueColor: AlwaysStoppedAnimation(
                  complete ? AppColors.green : AppColors.accent,
                ),
              ),
            ),
          ),
          if (complete)
            const Icon(Icons.check_rounded, color: AppColors.green, size: 24)
          else
            Text(
              label,
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                letterSpacing: -0.3,
              ),
            ),
        ],
      ),
    );
  }
}

/// A titled group of tasks rendered as one card, so the list reads as blocks
/// rather than an undifferentiated stream.
class _Section extends ConsumerWidget {
  const _Section({
    required this.title,
    required this.count,
    required this.accent,
    required this.tasks,
    required this.onToggle,
    this.showDate = false,
  });

  final String title;
  final int count;
  final Color accent;
  final List<TaskListItem> tasks;
  final void Function(String id) onToggle;
  final bool showDate;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return SliverToBoxAdapter(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(left: Gap.xs, bottom: Gap.sm),
              child: Row(
                children: [
                  Container(
                    width: 7,
                    height: 7,
                    decoration: BoxDecoration(
                      color: accent,
                      shape: BoxShape.circle,
                    ),
                  ),
                  const SizedBox(width: Gap.sm),
                  Text(
                    title.toUpperCase(),
                    style: TextStyle(
                      fontSize: 11.5,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 0.7,
                      color: accent,
                    ),
                  ),
                  const SizedBox(width: Gap.sm),
                  Text(
                    '$count',
                    style: TextStyle(
                      fontSize: 11.5,
                      fontWeight: FontWeight.w600,
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
            Card(
              // Clips the swipe backgrounds to the card's rounded corners.
              clipBehavior: Clip.antiAlias,
              child: Column(
                children: [
                  for (final (i, task) in tasks.indexed) ...[
                    if (i > 0) const Divider(indent: Gap.lg + 34 + Gap.md),
                    _SwipeableTask(
                      task: task,
                      showDate: showDate,
                      onToggle: () => onToggle(task.id),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A task row with swipe actions: right to reschedule to today, left to
/// delete. Both are the gestures a phone makes cheapest, and both are the
/// things most often done to a stale list.
class _SwipeableTask extends ConsumerWidget {
  const _SwipeableTask({
    required this.task,
    required this.onToggle,
    this.showDate = false,
  });

  final TaskListItem task;
  final VoidCallback onToggle;
  final bool showDate;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Dismissible(
      key: ValueKey(task.id),
      background: const _SwipeBackground(
        color: AppColors.accent,
        icon: Icons.today_rounded,
        label: 'Today',
        alignment: Alignment.centerLeft,
      ),
      secondaryBackground: const _SwipeBackground(
        color: AppColors.red,
        icon: Icons.delete_rounded,
        label: 'Delete',
        alignment: Alignment.centerRight,
      ),
      // Deleting is destructive, so it asks; rescheduling just happens and is
      // trivially undone by swiping again.
      confirmDismiss: (direction) async {
        if (direction == DismissDirection.startToEnd) {
          await _reschedule(context, ref);
          // Returning false keeps the row, which now belongs to Today.
          return false;
        }
        return _confirmDelete(context);
      },
      onDismissed: (_) => _delete(context, ref),
      child: TaskTile(
        task: task,
        showDate: showDate,
        onToggle: onToggle,
        onTap: () => context.push('/tasks/${task.id}'),
      ),
    );
  }

  Future<void> _reschedule(BuildContext context, WidgetRef ref) async {
    try {
      await ref.read(todayProvider.notifier).rescheduleToToday(task.id);
      if (context.mounted) showAppSnack(context, 'Moved to today');
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }

  Future<bool> _confirmDelete(BuildContext context) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Delete task?'),
        content: Text(task.title),
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
    return ok ?? false;
  }

  Future<void> _delete(BuildContext context, WidgetRef ref) async {
    try {
      await ref.read(todayProvider.notifier).delete(task.id);
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
      // The row is already gone from the tree; reload to put it back.
      ref.invalidate(todayProvider);
    }
  }
}

class _SwipeBackground extends StatelessWidget {
  const _SwipeBackground({
    required this.color,
    required this.icon,
    required this.label,
    required this.alignment,
  });

  final Color color;
  final IconData icon;
  final String label;
  final Alignment alignment;

  @override
  Widget build(BuildContext context) => Container(
    color: color,
    alignment: alignment,
    padding: const EdgeInsets.symmetric(horizontal: Gap.xl),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, color: Colors.white, size: 20),
        const SizedBox(width: Gap.sm),
        Text(
          label,
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w700,
            fontSize: 13,
          ),
        ),
      ],
    ),
  );
}
