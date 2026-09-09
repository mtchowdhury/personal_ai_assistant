import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import 'task_models.dart';

/// Talks to `/dtasks`. Every method returns parsed models — no raw maps escape
/// into the UI layer.
class TasksRepository {
  const TasksRepository(this._api);

  final ApiClient _api;

  /// `scheduledOn` is a calendar date; sending an offset would let the server
  /// land it on the wrong day, so it goes as a plain local date-time.
  static String _date(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-'
      '${d.month.toString().padLeft(2, '0')}-'
      '${d.day.toString().padLeft(2, '0')}T00:00:00';

  Future<TaskDashboard> dashboard() async =>
      TaskDashboard.fromJson(await _api.getObject('dtasks/dashboard'));

  Future<List<TaskStatus>> statuses() async =>
      (await _api.getList('dtasks/statuses'))
          .map(TaskStatus.fromJson)
          .toList(growable: false)
        ..sort((a, b) => a.sortOrder.compareTo(b.sortOrder));

  Future<List<TaskTag>> tags() async =>
      (await _api.getList('dtasks/tags')).map(TaskTag.fromJson).toList();

  /// The list view. `statusId` and `tag` repeat for multiple values, which Dio
  /// encodes correctly from a List.
  Future<List<TaskListItem>> list({
    List<String>? statusIds,
    List<String>? tags,
    String? priority,
    String? category,
    DateTime? from,
    DateTime? to,
    bool? unscheduled,
    bool includeDone = true,
    bool topLevelOnly = false,
    String? search,
    String sortBy = 'scheduledOn',
    String sortDir = 'asc',
    int take = 200,
  }) async {
    final query = <String, dynamic>{
      'includeDone': includeDone,
      'topLevelOnly': topLevelOnly,
      'sortBy': sortBy,
      'sortDir': sortDir,
      'take': take,
      if (statusIds != null && statusIds.isNotEmpty) 'statusId': statusIds,
      if (tags != null && tags.isNotEmpty) 'tag': tags,
      if (priority != null) 'priority': priority,
      if (category != null) 'category': category,
      if (from != null) 'from': _date(from),
      if (to != null) 'to': _date(to),
      if (unscheduled != null) 'unscheduled': unscheduled,
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    };
    return (await _api.getList('dtasks', query: query))
        .map(TaskListItem.fromJson)
        .toList(growable: false);
  }

  Future<TaskDetail> get(String id) async =>
      TaskDetail.fromJson(await _api.getObject('dtasks/$id'));

  Future<TaskDetail> create({
    required String title,
    String? notes,
    String? category,
    String? priority,
    DateTime? scheduledOn,
    String? timeOfDay,
    int? approxDurationMinutes,
    String? statusId,
    String? parentId,
    List<String> tags = const [],
  }) async {
    final json = await _api.post<Map<String, dynamic>>(
      'dtasks',
      body: {
        'title': title.trim(),
        if (notes != null && notes.trim().isNotEmpty) 'notes': notes.trim(),
        if (category != null) 'category': category,
        if (priority != null) 'priority': priority,
        if (scheduledOn != null) 'scheduledOn': _date(scheduledOn),
        if (timeOfDay != null) 'timeOfDay': timeOfDay,
        if (approxDurationMinutes != null)
          'approxDurationMinutes': approxDurationMinutes,
        if (statusId != null) 'statusId': statusId,
        if (parentId != null) 'parentId': parentId,
        'tags': tags,
        'source': 'mobile',
      },
    );
    return TaskDetail.fromJson(json);
  }

  /// Partial update. The server distinguishes "not supplied" from "clear it"
  /// via the explicit `clear*` flags, so nulls alone are not enough.
  Future<TaskDetail> update(
    String id, {
    String? title,
    String? notes,
    String? category,
    String? priority,
    DateTime? scheduledOn,
    bool clearScheduledOn = false,
    String? timeOfDay,
    bool clearTimeOfDay = false,
    int? approxDurationMinutes,
    String? statusId,
    String? parentId,
    bool clearParent = false,
    List<String>? tags,
  }) async {
    final json = await _api.put<Map<String, dynamic>>(
      'dtasks/$id',
      body: {
        if (title != null) 'title': title.trim(),
        if (notes != null) 'notes': notes,
        if (category != null) 'category': category,
        if (priority != null) 'priority': priority,
        if (scheduledOn != null) 'scheduledOn': _date(scheduledOn),
        if (clearScheduledOn) 'clearScheduledOn': true,
        if (timeOfDay != null) 'timeOfDay': timeOfDay,
        if (clearTimeOfDay) 'clearTimeOfDay': true,
        if (approxDurationMinutes != null)
          'approxDurationMinutes': approxDurationMinutes,
        if (statusId != null) 'statusId': statusId,
        if (parentId != null) 'parentId': parentId,
        if (clearParent) 'clearParent': true,
        if (tags != null) 'tags': tags,
      },
    );
    return TaskDetail.fromJson(json);
  }

  /// Flips done/not-done. The server picks the right status, so the client
  /// does not need to know which status counts as done.
  Future<TaskDetail> toggleDone(String id) async =>
      TaskDetail.fromJson(await _api.put<Map<String, dynamic>>('dtasks/$id/toggle'));

  Future<TaskDetail> move(
    String id, {
    String? statusId,
    DateTime? scheduledOn,
    bool clearScheduledOn = false,
    int? sortOrder,
  }) async {
    final json = await _api.put<Map<String, dynamic>>(
      'dtasks/$id/move',
      body: {
        if (statusId != null) 'statusId': statusId,
        if (scheduledOn != null) 'scheduledOn': _date(scheduledOn),
        if (clearScheduledOn) 'clearScheduledOn': true,
        if (sortOrder != null) 'sortOrder': sortOrder,
      },
    );
    return TaskDetail.fromJson(json);
  }

  Future<void> delete(String id) => _api.delete<void>('dtasks/$id');
}

final tasksRepositoryProvider = Provider<TasksRepository>(
  (ref) => TasksRepository(ref.watch(apiClientProvider)),
);
