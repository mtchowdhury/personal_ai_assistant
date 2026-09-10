import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'task_models.dart';
import 'tasks_controller.dart';
import 'tasks_repository.dart';

/// One task, loaded by id. A family so several tasks can be open across
/// navigation without clobbering each other, and auto-disposed so closing the
/// screen releases it.
class TaskDetailController extends AsyncNotifier<TaskDetail> {
  TaskDetailController(this.taskId);

  final String taskId;

  @override
  Future<TaskDetail> build() =>
      ref.watch(tasksRepositoryProvider).get(taskId);

  Future<void> refresh() async {
    state = AsyncData(await ref.read(tasksRepositoryProvider).get(taskId));
  }

  /// Any mutation also invalidates the Today screen, whose counters and lists
  /// may now be stale.
  void _invalidateLists() {
    ref.invalidate(todayProvider);
    ref.invalidate(taskListProvider);
  }

  Future<void> toggleDone() async {
    final updated = await ref.read(tasksRepositoryProvider).toggleDone(taskId);
    state = AsyncData(updated);
    _invalidateLists();
  }

  /// Toggling a subtask hits the same endpoint — subtasks are tasks — then
  /// reloads the parent so its progress count is right.
  Future<void> toggleSubtask(String subtaskId) async {
    await ref.read(tasksRepositoryProvider).toggleDone(subtaskId);
    await refresh();
    _invalidateLists();
  }

  Future<void> addSubtask(String title) async {
    await ref
        .read(tasksRepositoryProvider)
        .create(title: title, parentId: taskId);
    await refresh();
    _invalidateLists();
  }

  /// Named `save` rather than `update`: `AsyncNotifier` already defines an
  /// `update` for transforming state, and overriding it would be a type error.
  Future<void> save({
    String? title,
    String? notes,
    String? priority,
    DateTime? scheduledOn,
    bool clearScheduledOn = false,
    String? statusId,
    List<String>? tags,
    String? timeOfDay,
    bool clearTimeOfDay = false,
    int? approxDurationMinutes,
  }) async {
    final updated = await ref.read(tasksRepositoryProvider).update(
      taskId,
      title: title,
      notes: notes,
      priority: priority,
      scheduledOn: scheduledOn,
      clearScheduledOn: clearScheduledOn,
      statusId: statusId,
      tags: tags,
      timeOfDay: timeOfDay,
      clearTimeOfDay: clearTimeOfDay,
      approxDurationMinutes: approxDurationMinutes,
    );
    state = AsyncData(updated);
    _invalidateLists();
  }

  Future<void> delete() async {
    await ref.read(tasksRepositoryProvider).delete(taskId);
    _invalidateLists();
  }
}

final taskDetailProvider = AsyncNotifierProvider.autoDispose
    .family<TaskDetailController, TaskDetail, String>(
      TaskDetailController.new,
    );
