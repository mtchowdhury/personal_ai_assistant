import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'task_models.dart';
import 'tasks_repository.dart';

/// The Today screen's data. Kept as an `AsyncNotifier` so the UI can render
/// loading, error, and data from one object.
class TodayController extends AsyncNotifier<TaskDashboard> {
  @override
  Future<TaskDashboard> build() =>
      ref.watch(tasksRepositoryProvider).dashboard();

  /// Pull-to-refresh. Deliberately does not set a loading state: the list stays
  /// on screen and the platform's own spinner conveys progress.
  Future<void> refresh() async {
    final next = await ref.read(tasksRepositoryProvider).dashboard();
    state = AsyncData(next);
  }

  /// Toggles done, updating the list immediately and reconciling afterwards.
  ///
  /// The dashboard's counters depend on the outcome, so a full reload follows
  /// the optimistic flip rather than trying to recompute them client-side.
  Future<void> toggle(String taskId) async {
    final current = state.value;
    if (current != null) {
      state = AsyncData(_withToggled(current, taskId));
    }
    try {
      await ref.read(tasksRepositoryProvider).toggleDone(taskId);
    } catch (_) {
      // Put the optimistic change back before surfacing the reload.
      if (current != null) state = AsyncData(current);
      rethrow;
    } finally {
      await refresh();
    }
  }

  /// Applies a done-flip locally so the checkbox does not lag the tap.
  TaskDashboard _withToggled(TaskDashboard d, String taskId) {
    var todayDone = d.todayDoneCount;

    List<TaskListItem> flip(List<TaskListItem> list, {required bool counts}) => [
      for (final t in list)
        if (t.id == taskId)
          () {
            if (counts) todayDone += t.isDone ? -1 : 1;
            return t.copyWith(isDone: !t.isDone);
          }()
        else
          t,
    ];

    final todayTasks = flip(d.todayTasks, counts: true);
    final overdueTasks = flip(d.overdueTasks, counts: false);

    return TaskDashboard(
      overdueCount: d.overdueCount,
      todayCount: d.todayCount,
      todayDoneCount: todayDone.clamp(0, d.todayCount),
      upcomingCount: d.upcomingCount,
      unscheduledCount: d.unscheduledCount,
      todayTasks: todayTasks,
      overdueTasks: overdueTasks,
    );
  }

  /// Adds a task and reloads. Returns nothing useful — the list is the truth.
  Future<void> quickAdd({
    required String title,
    DateTime? scheduledOn,
    String? priority,
    List<String> tags = const [],
  }) async {
    await ref.read(tasksRepositoryProvider).create(
      title: title,
      scheduledOn: scheduledOn,
      priority: priority,
      tags: tags,
    );
    await refresh();
  }

  Future<void> delete(String taskId) async {
    await ref.read(tasksRepositoryProvider).delete(taskId);
    await refresh();
  }

  /// Moves an overdue task to today — the common way to clear a backlog.
  Future<void> rescheduleToToday(String taskId) async {
    await ref.read(tasksRepositoryProvider).move(taskId, scheduledOn: today());
    await refresh();
  }
}

final todayProvider = AsyncNotifierProvider<TodayController, TaskDashboard>(
  TodayController.new,
);

/// Statuses and tags change rarely, so they are cached for the session and
/// shared by the quick-add sheet, filters, and the board.
final taskStatusesProvider = FutureProvider<List<TaskStatus>>(
  (ref) => ref.watch(tasksRepositoryProvider).statuses(),
);

final taskTagsProvider = FutureProvider<List<TaskTag>>(
  (ref) => ref.watch(tasksRepositoryProvider).tags(),
);

/// Filters for the browse-all list, held separately from the Today view.
class TaskListFilter {
  const TaskListFilter({
    this.search,
    this.tags = const [],
    this.includeDone = false,
    this.unscheduledOnly = false,
  });

  final String? search;
  final List<String> tags;
  final bool includeDone;
  final bool unscheduledOnly;

  bool get isActive =>
      (search?.isNotEmpty ?? false) ||
      tags.isNotEmpty ||
      includeDone ||
      unscheduledOnly;

  TaskListFilter copyWith({
    String? search,
    bool clearSearch = false,
    List<String>? tags,
    bool? includeDone,
    bool? unscheduledOnly,
  }) => TaskListFilter(
    search: clearSearch ? null : (search ?? this.search),
    tags: tags ?? this.tags,
    includeDone: includeDone ?? this.includeDone,
    unscheduledOnly: unscheduledOnly ?? this.unscheduledOnly,
  );

  @override
  bool operator ==(Object other) =>
      other is TaskListFilter &&
      other.search == search &&
      other.includeDone == includeDone &&
      other.unscheduledOnly == unscheduledOnly &&
      other.tags.join(',') == tags.join(',');

  @override
  int get hashCode =>
      Object.hash(search, includeDone, unscheduledOnly, tags.join(','));
}

class TaskFilterController extends Notifier<TaskListFilter> {
  @override
  TaskListFilter build() => const TaskListFilter();

  void setSearch(String? value) => state = (value == null || value.isEmpty)
      ? state.copyWith(clearSearch: true)
      : state.copyWith(search: value);

  void toggleTag(String tag) {
    final next = List.of(state.tags);
    if (!next.remove(tag)) next.add(tag);
    state = state.copyWith(tags: next);
  }

  void setIncludeDone(bool v) => state = state.copyWith(includeDone: v);

  void setUnscheduledOnly(bool v) => state = state.copyWith(unscheduledOnly: v);

  void clear() => state = const TaskListFilter();
}

final taskFilterProvider =
    NotifierProvider<TaskFilterController, TaskListFilter>(
      TaskFilterController.new,
    );

/// The filtered "all tasks" list. Auto-disposed so leaving the screen drops
/// the results rather than holding a large list in memory.
final taskListProvider = FutureProvider.autoDispose<List<TaskListItem>>((
  ref,
) async {
  final filter = ref.watch(taskFilterProvider);
  return ref.watch(tasksRepositoryProvider).list(
    search: filter.search,
    tags: filter.tags,
    includeDone: filter.includeDone,
    unscheduled: filter.unscheduledOnly ? true : null,
    topLevelOnly: true,
  );
});

/// The month the calendar is showing. Held outside the data provider so
/// swiping months does not rebuild the whole feature.
class CalendarMonthController extends Notifier<DateTime> {
  @override
  DateTime build() {
    final now = DateTime.now();
    return DateTime(now.year, now.month);
  }

  void next() => state = DateTime(state.year, state.month + 1);
  void previous() => state = DateTime(state.year, state.month - 1);
  void jumpToToday() {
    final now = DateTime.now();
    state = DateTime(now.year, now.month);
  }
}

final calendarMonthProvider =
    NotifierProvider<CalendarMonthController, DateTime>(
      CalendarMonthController.new,
    );

/// Tasks for the visible month, bucketed by day so the grid can look up a
/// date in constant time instead of filtering the list per cell.
final calendarTasksProvider = FutureProvider.autoDispose<
  Map<DateTime, List<TaskListItem>>
>((ref) async {
  final month = ref.watch(calendarMonthProvider);
  final tasks = await ref
      .watch(tasksRepositoryProvider)
      .calendar(year: month.year, month: month.month);

  final byDay = <DateTime, List<TaskListItem>>{};
  for (final t in tasks) {
    final d = t.scheduledOn;
    if (d == null) continue;
    byDay.putIfAbsent(dateOnly(d), () => []).add(t);
  }
  return byDay;
});

/// The day the calendar has selected, whose tasks are listed under the grid.
class SelectedDayController extends Notifier<DateTime> {
  @override
  DateTime build() => today();

  void select(DateTime day) => state = dateOnly(day);
}

final selectedDayProvider = NotifierProvider<SelectedDayController, DateTime>(
  SelectedDayController.new,
);
