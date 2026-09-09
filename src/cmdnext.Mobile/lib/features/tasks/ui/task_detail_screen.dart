import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/task_detail_controller.dart';
import '../data/task_models.dart';
import '../data/tasks_controller.dart';

/// Full view of one task: notes, subtasks, and every field editable in place.
///
/// Edits commit on change rather than behind a save button — on a phone, a
/// modal edit mode for a title and three dropdowns is more friction than the
/// data warrants.
class TaskDetailScreen extends ConsumerWidget {
  const TaskDetailScreen({super.key, required this.taskId});

  final String taskId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(taskDetailProvider(taskId));

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
          onRetry: () => ref.invalidate(taskDetailProvider(taskId)),
        ),
        data: (task) => _Body(taskId: taskId, task: task),
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Delete task?'),
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
    if (confirmed != true) return;

    try {
      await ref.read(taskDetailProvider(taskId).notifier).delete();
      if (context.mounted) Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }
}

class _Body extends ConsumerStatefulWidget {
  const _Body({required this.taskId, required this.task});

  final String taskId;
  final TaskDetail task;

  @override
  ConsumerState<_Body> createState() => _BodyState();
}

class _BodyState extends ConsumerState<_Body> {
  late final _title = TextEditingController(text: widget.task.title);
  late final _notes = TextEditingController(text: widget.task.notes ?? '');
  final _subtask = TextEditingController();
  bool _busy = false;

  @override
  void dispose() {
    _title.dispose();
    _notes.dispose();
    _subtask.dispose();
    super.dispose();
  }

  TaskDetailController get _controller =>
      ref.read(taskDetailProvider(widget.taskId).notifier);

  /// Wraps a mutation with error reporting, so no failure is silent.
  Future<void> _run(Future<void> Function() action) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await action();
    } on ApiException catch (e) {
      if (mounted) showAppSnack(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final task = widget.task;
    final scheme = Theme.of(context).colorScheme;
    final statuses = ref.watch(taskStatusesProvider).value ?? const [];
    final allTags = ref.watch(taskTagsProvider).value ?? const [];

    return ListView(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.xxl),
      children: [
        // Title, with the done control beside it.
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: Gap.sm),
              child: GestureDetector(
                onTap: () {
                  HapticFeedback.lightImpact();
                  _run(_controller.toggleDone);
                },
                behavior: HitTestBehavior.opaque,
                child: SizedBox(
                  width: 40,
                  height: 40,
                  child: Center(
                    child: Container(
                      width: 26,
                      height: 26,
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        color: task.isDone
                            ? AppColors.parse(task.statusColor)
                            : Colors.transparent,
                        border: Border.all(
                          color: AppColors.parse(task.statusColor),
                          width: 2,
                        ),
                      ),
                      child: task.isDone
                          ? const Icon(
                              Icons.check_rounded,
                              size: 16,
                              color: Colors.white,
                            )
                          : null,
                    ),
                  ),
                ),
              ),
            ),
            Expanded(
              child: TextField(
                controller: _title,
                maxLines: null,
                textCapitalization: TextCapitalization.sentences,
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w700,
                  letterSpacing: -0.4,
                  height: 1.25,
                  decoration: task.isDone ? TextDecoration.lineThrough : null,
                  color: task.isDone ? scheme.onSurfaceVariant : null,
                ),
                decoration: const InputDecoration(
                  border: InputBorder.none,
                  enabledBorder: InputBorder.none,
                  focusedBorder: InputBorder.none,
                  filled: false,
                  isDense: true,
                  contentPadding: EdgeInsets.symmetric(vertical: Gap.sm),
                ),
                // Committing on focus loss avoids a write on every keystroke.
                onTapOutside: (_) {
                  FocusScope.of(context).unfocus();
                  final v = _title.text.trim();
                  if (v.isNotEmpty && v != task.title) {
                    _run(() => _controller.save(title: v));
                  }
                },
              ),
            ),
          ],
        ),

        if (task.parentTitle case final p?) ...[
          const SizedBox(height: Gap.xs),
          Row(
            children: [
              Icon(
                Icons.subdirectory_arrow_right_rounded,
                size: 15,
                color: scheme.onSurfaceVariant,
              ),
              const SizedBox(width: Gap.xs),
              Expanded(
                child: Text(
                  'Subtask of $p',
                  style: TextStyle(
                    fontSize: 13,
                    color: scheme.onSurfaceVariant,
                  ),
                ),
              ),
            ],
          ),
        ],

        const SizedBox(height: Gap.lg),

        // The editable properties, as rows rather than a form.
        Card(
          child: Column(
            children: [
              _Row(
                icon: Icons.event_rounded,
                label: 'Date',
                value: task.scheduledOn == null
                    ? 'Unscheduled'
                    : DateFormat('EEE d MMM').format(task.scheduledOn!),
                muted: task.scheduledOn == null,
                danger:
                    task.scheduledOn != null &&
                    !task.isDone &&
                    task.scheduledOn!.isBefore(today()),
                onTap: _pickDate,
              ),
              const Divider(indent: 52),
              _Row(
                icon: Icons.flag_outlined,
                label: 'Priority',
                value: task.priority.wire,
                valueColor: task.priority.color,
                onTap: () => _pickPriority(task),
              ),
              const Divider(indent: 52),
              _Row(
                icon: Icons.radio_button_checked_rounded,
                label: 'Status',
                value: task.statusName,
                valueColor: AppColors.parse(task.statusColor),
                onTap: statuses.isEmpty ? null : () => _pickStatus(task),
              ),
              const Divider(indent: 52),
              _Row(
                icon: Icons.sell_outlined,
                label: 'Tags',
                value: task.tags.isEmpty ? 'None' : task.tags.join(', '),
                muted: task.tags.isEmpty,
                onTap: allTags.isEmpty ? null : () => _pickTags(task, allTags),
              ),
            ],
          ),
        ),

        const SizedBox(height: Gap.lg),
        _SectionLabel('Notes'),
        Card(
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: Gap.md,
              vertical: Gap.sm,
            ),
            child: TextField(
              controller: _notes,
              maxLines: null,
              minLines: 3,
              textCapitalization: TextCapitalization.sentences,
              style: const TextStyle(fontSize: 15, height: 1.45),
              decoration: InputDecoration(
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                filled: false,
                hintText: 'Add notes…',
                hintStyle: TextStyle(color: scheme.onSurfaceVariant),
                contentPadding: const EdgeInsets.symmetric(vertical: Gap.sm),
              ),
              onTapOutside: (_) {
                FocusScope.of(context).unfocus();
                if (_notes.text != (task.notes ?? '')) {
                  _run(() => _controller.save(notes: _notes.text));
                }
              },
            ),
          ),
        ),

        const SizedBox(height: Gap.lg),
        _SectionLabel(
          task.subtasks.isEmpty
              ? 'Subtasks'
              : 'Subtasks  ${task.subtasks.where((s) => s.isDone).length}'
                    '/${task.subtasks.length}',
        ),
        Card(
          child: Column(
            children: [
              for (final (i, sub) in task.subtasks.indexed) ...[
                if (i > 0) const Divider(indent: 52),
                InkWell(
                  onTap: () {
                    HapticFeedback.lightImpact();
                    _run(() => _controller.toggleSubtask(sub.id));
                  },
                  child: Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: Gap.md,
                      vertical: Gap.md,
                    ),
                    child: Row(
                      children: [
                        Icon(
                          sub.isDone
                              ? Icons.check_circle_rounded
                              : Icons.circle_outlined,
                          size: 21,
                          color: sub.isDone
                              ? AppColors.parse(sub.statusColor)
                              : scheme.onSurfaceVariant,
                        ),
                        const SizedBox(width: Gap.md),
                        Expanded(
                          child: Text(
                            sub.title,
                            style: TextStyle(
                              fontSize: 15,
                              decoration: sub.isDone
                                  ? TextDecoration.lineThrough
                                  : null,
                              color: sub.isDone
                                  ? scheme.onSurfaceVariant
                                  : null,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
              if (task.subtasks.isNotEmpty) const Divider(indent: 52),
              Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: Gap.md,
                  vertical: Gap.xs,
                ),
                child: Row(
                  children: [
                    Icon(
                      Icons.add_rounded,
                      size: 21,
                      color: scheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: Gap.md),
                    Expanded(
                      child: TextField(
                        controller: _subtask,
                        textCapitalization: TextCapitalization.sentences,
                        textInputAction: TextInputAction.done,
                        style: const TextStyle(fontSize: 15),
                        decoration: InputDecoration(
                          border: InputBorder.none,
                          enabledBorder: InputBorder.none,
                          focusedBorder: InputBorder.none,
                          filled: false,
                          isDense: true,
                          hintText: 'Add a subtask',
                          hintStyle: TextStyle(color: scheme.onSurfaceVariant),
                        ),
                        onSubmitted: (v) {
                          final t = v.trim();
                          if (t.isEmpty) return;
                          _subtask.clear();
                          _run(() => _controller.addSubtask(t));
                        },
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),

        if (task.createdOn != null) ...[
          const SizedBox(height: Gap.xl),
          Text(
            'Created ${DateFormat('d MMM y').format(task.createdOn!)}'
            '${task.source != 'manual' ? ' · via ${task.source}' : ''}',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
          ),
        ],
      ],
    );
  }

  Future<void> _pickDate() async {
    final task = widget.task;
    final picked = await showDatePicker(
      context: context,
      initialDate: task.scheduledOn ?? today(),
      firstDate: DateTime(today().year - 2),
      lastDate: DateTime(today().year + 5),
      // A way to clear the date, which the stock picker does not offer.
      helpText: 'Schedule task',
    );
    if (picked == null) return;
    await _run(() => _controller.save(scheduledOn: dateOnly(picked)));
  }

  Future<void> _pickPriority(TaskDetail task) async {
    final picked = await showModalBottomSheet<TaskPriority>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final p in TaskPriority.values)
              ListTile(
                leading: Icon(Icons.flag_rounded, color: p.color),
                title: Text(p.wire),
                trailing: task.priority == p
                    ? const Icon(Icons.check_rounded, color: AppColors.accent)
                    : null,
                onTap: () => Navigator.of(sheetContext).pop(p),
              ),
          ],
        ),
      ),
    );
    if (picked == null || picked == task.priority) return;
    await _run(() => _controller.save(priority: picked.wire));
  }

  Future<void> _pickStatus(TaskDetail task) async {
    final statuses = ref.read(taskStatusesProvider).value ?? const [];
    final picked = await showModalBottomSheet<String>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final s in statuses)
              ListTile(
                leading: Container(
                  width: 12,
                  height: 12,
                  decoration: BoxDecoration(
                    color: s.displayColor,
                    shape: BoxShape.circle,
                  ),
                ),
                title: Text(s.name),
                trailing: task.statusId == s.id
                    ? const Icon(Icons.check_rounded, color: AppColors.accent)
                    : null,
                onTap: () => Navigator.of(sheetContext).pop(s.id),
              ),
          ],
        ),
      ),
    );
    if (picked == null || picked == task.statusId) return;
    await _run(() => _controller.save(statusId: picked));
  }

  Future<void> _pickTags(TaskDetail task, List<TaskTag> allTags) async {
    final selected = task.tags.toSet();
    final result = await showModalBottomSheet<Set<String>>(
      context: context,
      builder: (sheetContext) => StatefulBuilder(
        builder: (builderContext, setSheetState) => SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (final t in allTags)
                CheckboxListTile(
                  value: selected.contains(t.name),
                  title: Text(t.name),
                  activeColor: t.displayColor,
                  onChanged: (on) => setSheetState(() {
                    if (on == true) {
                      selected.add(t.name);
                    } else {
                      selected.remove(t.name);
                    }
                  }),
                ),
              Padding(
                padding: const EdgeInsets.all(Gap.lg),
                child: FilledButton(
                  onPressed: () =>
                      Navigator.of(builderContext).pop(selected),
                  child: const Text('Done'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
    if (result == null) return;
    if (result.join(',') == task.tags.join(',')) return;
    await _run(() => _controller.save(tags: result.toList()));
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(left: Gap.xs, bottom: Gap.sm),
    child: Text(
      text.toUpperCase(),
      style: TextStyle(
        fontSize: 11.5,
        fontWeight: FontWeight.w800,
        letterSpacing: 0.7,
        color: Theme.of(context).colorScheme.onSurfaceVariant,
      ),
    ),
  );
}

class _Row extends StatelessWidget {
  const _Row({
    required this.icon,
    required this.label,
    required this.value,
    this.onTap,
    this.valueColor,
    this.muted = false,
    this.danger = false,
  });

  final IconData icon;
  final String label;
  final String value;
  final VoidCallback? onTap;
  final Color? valueColor;
  final bool muted;
  final bool danger;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: Gap.md,
          vertical: Gap.md + 2,
        ),
        child: Row(
          children: [
            Icon(icon, size: 20, color: scheme.onSurfaceVariant),
            const SizedBox(width: Gap.md),
            Text(
              label,
              style: TextStyle(fontSize: 14, color: scheme.onSurfaceVariant),
            ),
            const Spacer(),
            Flexible(
              child: Text(
                value,
                textAlign: TextAlign.end,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 14.5,
                  fontWeight: FontWeight.w600,
                  color: danger
                      ? AppColors.red
                      : muted
                      ? scheme.onSurfaceVariant
                      : valueColor,
                ),
              ),
            ),
            if (onTap != null) ...[
              const SizedBox(width: Gap.xs),
              Icon(
                Icons.chevron_right_rounded,
                size: 19,
                color: scheme.onSurfaceVariant,
              ),
            ],
          ],
        ),
      ),
    );
  }
}
