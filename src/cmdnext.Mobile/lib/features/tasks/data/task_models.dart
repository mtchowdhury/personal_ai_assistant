import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';

/// Parses a **calendar date** from the API (`scheduledOn`, `dueOn`).
///
/// The server stores these in a `timestamp with time zone` column and stamps
/// midnight as UTC (`DTaskService.NormalizeDate`), so they arrive as
/// `2026-09-09T00:00:00Z`. They are dates, not instants: converting to local
/// time shifts them across midnight and makes a task scheduled for today read
/// as yesterday from a negative UTC offset. So the date parts are taken
/// verbatim and the zone is discarded.
DateTime? parseApiDate(Object? v) {
  if (v == null) return null;
  final s = v.toString();
  if (s.isEmpty) return null;
  final parsed = DateTime.tryParse(s);
  if (parsed == null) return null;
  // `DateTime.parse` on a `Z` string yields a UTC DateTime; read its Y/M/D as
  // written rather than re-projecting it into the device's zone.
  final utc = parsed.isUtc ? parsed : parsed.toUtc();
  return DateTime(utc.year, utc.month, utc.day);
}

/// Parses a real instant (`createdOn`, `completedOn`), where the timezone does
/// matter — unlike [parseApiDate], these are points in time, not dates.
DateTime? parseApiInstant(Object? v) {
  if (v == null) return null;
  final s = v.toString();
  if (s.isEmpty) return null;
  return DateTime.tryParse(s)?.toLocal();
}

/// Date with the time stripped — the unit tasks are actually scheduled in.
DateTime dateOnly(DateTime d) => DateTime(d.year, d.month, d.day);

DateTime today() => dateOnly(DateTime.now());

class TaskStatus {
  const TaskStatus({
    required this.id,
    required this.name,
    required this.sortOrder,
    required this.isDone,
    required this.isProtected,
    required this.taskCount,
    this.color,
  });

  final String id;
  final String name;
  final int sortOrder;
  final bool isDone;
  final bool isProtected;
  final int taskCount;
  final String? color;

  Color get displayColor => AppColors.parse(color);

  factory TaskStatus.fromJson(Map<String, dynamic> j) => TaskStatus(
    id: (j['id'] ?? '').toString(),
    name: (j['name'] ?? '').toString(),
    sortOrder: (j['sortOrder'] as num?)?.toInt() ?? 0,
    isDone: j['isDone'] == true,
    isProtected: j['isProtected'] == true,
    taskCount: (j['taskCount'] as num?)?.toInt() ?? 0,
    color: j['color'] as String?,
  );
}

class TaskTag {
  const TaskTag({
    required this.id,
    required this.name,
    required this.taskCount,
    this.color,
  });

  final String id;
  final String name;
  final int taskCount;
  final String? color;

  Color get displayColor => AppColors.parse(color, fallback: AppColors.accent);

  factory TaskTag.fromJson(Map<String, dynamic> j) => TaskTag(
    id: (j['id'] ?? '').toString(),
    name: (j['name'] ?? '').toString(),
    taskCount: (j['taskCount'] as num?)?.toInt() ?? 0,
    color: j['color'] as String?,
  );
}

/// Priority is a free string server-side; these are the values the web client
/// produces. Unknown values degrade to medium rather than throwing.
enum TaskPriority {
  low('Low'),
  medium('Medium'),
  high('High'),
  urgent('Urgent');

  const TaskPriority(this.wire);
  final String wire;

  static TaskPriority parse(String? v) => switch (v?.toLowerCase()) {
    'low' => TaskPriority.low,
    'high' => TaskPriority.high,
    'urgent' => TaskPriority.urgent,
    _ => TaskPriority.medium,
  };

  Color get color => switch (this) {
    TaskPriority.low => AppColors.slate,
    TaskPriority.medium => AppColors.accent,
    TaskPriority.high => AppColors.amber,
    TaskPriority.urgent => AppColors.red,
  };

  /// Only the ends of the scale are worth a badge; medium is the default and
  /// badging every task would make the list noisy.
  bool get isNotable => this == TaskPriority.high || this == TaskPriority.urgent;
}

/// A task as it appears in a list. Mirrors `TaskListItemDto`.
class TaskListItem {
  const TaskListItem({
    required this.id,
    required this.title,
    required this.category,
    required this.priority,
    required this.statusId,
    required this.statusName,
    required this.isDone,
    required this.tags,
    required this.hasNotes,
    required this.subtaskCount,
    required this.subtaskDoneCount,
    required this.source,
    required this.sortOrder,
    this.statusColor,
    this.scheduledOn,
    this.timeOfDay,
    this.approxDurationMinutes,
    this.parentId,
    this.completedOn,
  });

  final String id;
  final String title;
  final String category;
  final TaskPriority priority;
  final DateTime? scheduledOn;
  final String? timeOfDay;
  final int? approxDurationMinutes;
  final String statusId;
  final String statusName;
  final String? statusColor;
  final bool isDone;
  final String? parentId;
  final List<String> tags;
  final bool hasNotes;
  final int subtaskCount;
  final int subtaskDoneCount;
  final String source;
  final DateTime? completedOn;
  final int sortOrder;

  Color get statusDisplayColor => AppColors.parse(statusColor);

  bool get hasSubtasks => subtaskCount > 0;

  /// Overdue means scheduled strictly before today and not finished.
  bool get isOverdue {
    final d = scheduledOn;
    if (d == null || isDone) return false;
    return dateOnly(d).isBefore(today());
  }

  bool get isToday {
    final d = scheduledOn;
    return d != null && dateOnly(d) == today();
  }

  factory TaskListItem.fromJson(Map<String, dynamic> j) => TaskListItem(
    id: (j['id'] ?? '').toString(),
    title: (j['title'] ?? '').toString(),
    category: (j['category'] ?? 'Task').toString(),
    priority: TaskPriority.parse(j['priority'] as String?),
    scheduledOn: parseApiDate(j['scheduledOn']),
    timeOfDay: j['timeOfDay'] as String?,
    approxDurationMinutes: (j['approxDurationMinutes'] as num?)?.toInt(),
    statusId: (j['statusId'] ?? '').toString(),
    statusName: (j['statusName'] ?? '').toString(),
    statusColor: j['statusColor'] as String?,
    isDone: j['isDone'] == true,
    parentId: j['parentId']?.toString(),
    tags: ((j['tags'] as List?) ?? const [])
        .map((e) => e.toString())
        .toList(growable: false),
    hasNotes: j['hasNotes'] == true,
    subtaskCount: (j['subtaskCount'] as num?)?.toInt() ?? 0,
    subtaskDoneCount: (j['subtaskDoneCount'] as num?)?.toInt() ?? 0,
    source: (j['source'] ?? 'manual').toString(),
    completedOn: parseApiInstant(j['completedOn']),
    sortOrder: (j['sortOrder'] as num?)?.toInt() ?? 0,
  );

  /// Local echo for optimistic toggling, so the checkbox responds instantly
  /// instead of waiting for the round trip.
  TaskListItem copyWith({
    bool? isDone,
    String? statusId,
    String? statusName,
    String? statusColor,
    DateTime? scheduledOn,
    bool clearScheduledOn = false,
  }) => TaskListItem(
    id: id,
    title: title,
    category: category,
    priority: priority,
    scheduledOn: clearScheduledOn ? null : (scheduledOn ?? this.scheduledOn),
    timeOfDay: timeOfDay,
    approxDurationMinutes: approxDurationMinutes,
    statusId: statusId ?? this.statusId,
    statusName: statusName ?? this.statusName,
    statusColor: statusColor ?? this.statusColor,
    isDone: isDone ?? this.isDone,
    parentId: parentId,
    tags: tags,
    hasNotes: hasNotes,
    subtaskCount: subtaskCount,
    subtaskDoneCount: subtaskDoneCount,
    source: source,
    completedOn: completedOn,
    sortOrder: sortOrder,
  );
}

/// A subtask, as nested inside a full task.
class Subtask {
  const Subtask({
    required this.id,
    required this.title,
    required this.statusId,
    required this.statusName,
    required this.isDone,
    required this.priority,
    required this.sortOrder,
    this.statusColor,
    this.scheduledOn,
  });

  final String id;
  final String title;
  final String statusId;
  final String statusName;
  final String? statusColor;
  final bool isDone;
  final TaskPriority priority;
  final DateTime? scheduledOn;
  final int sortOrder;

  factory Subtask.fromJson(Map<String, dynamic> j) => Subtask(
    id: (j['id'] ?? '').toString(),
    title: (j['title'] ?? '').toString(),
    statusId: (j['statusId'] ?? '').toString(),
    statusName: (j['statusName'] ?? '').toString(),
    statusColor: j['statusColor'] as String?,
    isDone: j['isDone'] == true,
    priority: TaskPriority.parse(j['priority'] as String?),
    scheduledOn: parseApiDate(j['scheduledOn']),
    sortOrder: (j['sortOrder'] as num?)?.toInt() ?? 0,
  );
}

/// The full task, including notes and subtasks. Mirrors `TaskDto`.
class TaskDetail {
  const TaskDetail({
    required this.id,
    required this.title,
    required this.category,
    required this.priority,
    required this.statusId,
    required this.statusName,
    required this.isDone,
    required this.tags,
    required this.subtasks,
    required this.source,
    this.notes,
    this.statusColor,
    this.scheduledOn,
    this.timeOfDay,
    this.approxDurationMinutes,
    this.parentId,
    this.parentTitle,
    this.completedOn,
    this.createdOn,
  });

  final String id;
  final String title;
  final String? notes;
  final String category;
  final TaskPriority priority;
  final DateTime? scheduledOn;
  final String? timeOfDay;
  final int? approxDurationMinutes;
  final String statusId;
  final String statusName;
  final String? statusColor;
  final bool isDone;
  final String? parentId;
  final String? parentTitle;
  final List<String> tags;
  final DateTime? completedOn;
  final String source;
  final DateTime? createdOn;
  final List<Subtask> subtasks;

  factory TaskDetail.fromJson(Map<String, dynamic> j) => TaskDetail(
    id: (j['id'] ?? '').toString(),
    title: (j['title'] ?? '').toString(),
    notes: j['notes'] as String?,
    category: (j['category'] ?? 'Task').toString(),
    priority: TaskPriority.parse(j['priority'] as String?),
    scheduledOn: parseApiDate(j['scheduledOn']),
    timeOfDay: j['timeOfDay'] as String?,
    approxDurationMinutes: (j['approxDurationMinutes'] as num?)?.toInt(),
    statusId: (j['statusId'] ?? '').toString(),
    statusName: (j['statusName'] ?? '').toString(),
    statusColor: j['statusColor'] as String?,
    isDone: j['isDone'] == true,
    parentId: j['parentId']?.toString(),
    parentTitle: j['parentTitle'] as String?,
    tags: ((j['tags'] as List?) ?? const [])
        .map((e) => e.toString())
        .toList(growable: false),
    completedOn: parseApiInstant(j['completedOn']),
    source: (j['source'] ?? 'manual').toString(),
    createdOn: parseApiInstant(j['createdOn']),
    subtasks:
        ((j['subtasks'] as List?) ?? const [])
            .whereType<Map>()
            .map((e) => Subtask.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false)
          ..sort((a, b) => a.sortOrder.compareTo(b.sortOrder)),
  );
}

/// The `/dtasks/dashboard` payload that drives the Today screen.
class TaskDashboard {
  const TaskDashboard({
    required this.overdueCount,
    required this.todayCount,
    required this.todayDoneCount,
    required this.upcomingCount,
    required this.unscheduledCount,
    required this.todayTasks,
    required this.overdueTasks,
  });

  final int overdueCount;
  final int todayCount;
  final int todayDoneCount;
  final int upcomingCount;
  final int unscheduledCount;
  final List<TaskListItem> todayTasks;
  final List<TaskListItem> overdueTasks;

  /// 0..1 for the day's progress ring. A day with nothing scheduled reads as
  /// complete rather than empty, so the ring is not stuck at zero.
  double get todayProgress =>
      todayCount == 0 ? 1 : (todayDoneCount / todayCount).clamp(0, 1);

  bool get isClear => todayCount == 0 && overdueCount == 0;

  static List<TaskListItem> _list(Object? v) =>
      ((v as List?) ?? const [])
          .whereType<Map>()
          .map((e) => TaskListItem.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);

  factory TaskDashboard.fromJson(Map<String, dynamic> j) => TaskDashboard(
    overdueCount: (j['overdueCount'] as num?)?.toInt() ?? 0,
    todayCount: (j['todayCount'] as num?)?.toInt() ?? 0,
    todayDoneCount: (j['todayDoneCount'] as num?)?.toInt() ?? 0,
    upcomingCount: (j['upcomingCount'] as num?)?.toInt() ?? 0,
    unscheduledCount: (j['unscheduledCount'] as num?)?.toInt() ?? 0,
    todayTasks: _list(j['today']),
    overdueTasks: _list(j['overdue']),
  );
}

/// One column of the kanban board — a status plus the tasks sitting in it.
/// The server returns every status, empty ones included, so a task can always
/// be moved into any column.
class TaskBoardColumn {
  const TaskBoardColumn({
    required this.statusId,
    required this.statusName,
    required this.isDone,
    required this.sortOrder,
    required this.tasks,
    this.statusColor,
  });

  final String statusId;
  final String statusName;
  final String? statusColor;
  final bool isDone;
  final int sortOrder;
  final List<TaskListItem> tasks;

  Color get displayColor => AppColors.parse(statusColor);

  /// Same column, different tasks — used when a card is moved optimistically.
  TaskBoardColumn withTasks(List<TaskListItem> next) => TaskBoardColumn(
    statusId: statusId,
    statusName: statusName,
    statusColor: statusColor,
    isDone: isDone,
    sortOrder: sortOrder,
    tasks: next,
  );

  factory TaskBoardColumn.fromJson(Map<String, dynamic> j) => TaskBoardColumn(
    statusId: (j['statusId'] ?? '').toString(),
    statusName: (j['statusName'] ?? '').toString(),
    statusColor: j['statusColor'] as String?,
    isDone: j['isDone'] == true,
    sortOrder: (j['sortOrder'] as num?)?.toInt() ?? 0,
    tasks: ((j['tasks'] as List?) ?? const [])
        .whereType<Map>()
        .map((e) => TaskListItem.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false),
  );
}
