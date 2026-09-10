import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/task_models.dart';
import '../data/tasks_controller.dart';
import 'quick_add_sheet.dart';

/// The kanban board.
///
/// The web board is drag-and-drop across side-by-side columns. That does not
/// survive a phone: columns would be ~80pt wide, and dragging a card while the
/// page also scrolls horizontally fights the platform's own gestures. So the
/// board becomes one column at a time — swipe or tap between statuses — and a
/// card moves by picking a destination rather than by being dragged to one.
class BoardScreen extends ConsumerStatefulWidget {
  const BoardScreen({super.key});

  @override
  ConsumerState<BoardScreen> createState() => _BoardScreenState();
}

class _BoardScreenState extends ConsumerState<BoardScreen> {
  final _controller = PageController();
  int _page = 0;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(boardProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Board')),
      body: async.when(
        loading: () => const LoadingState(),
        error: (e, _) =>
            ErrorState(error: e, onRetry: () => ref.invalidate(boardProvider)),
        data: (columns) {
          if (columns.isEmpty) {
            return const EmptyState(
              icon: Icons.view_kanban_outlined,
              title: 'No statuses',
              message: 'Add a status in the web app to use the board.',
            );
          }
          final page = _page.clamp(0, columns.length - 1);

          return Column(
            children: [
              _ColumnTabs(
                columns: columns,
                current: page,
                onSelect: (i) => _controller.animateToPage(
                  i,
                  duration: const Duration(milliseconds: 240),
                  curve: Curves.easeOutCubic,
                ),
              ),
              Expanded(
                child: PageView.builder(
                  controller: _controller,
                  itemCount: columns.length,
                  onPageChanged: (i) => setState(() => _page = i),
                  itemBuilder: (context, i) => _ColumnView(
                    column: columns[i],
                    allColumns: columns,
                  ),
                ),
              ),
            ],
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () async {
          final added = await showQuickAddSheet(context);
          if (added) ref.invalidate(boardProvider);
        },
        tooltip: 'Add task',
        child: const Icon(Icons.add_rounded),
      ),
    );
  }
}

/// Scrollable status tabs, each carrying its count — the board's overview,
/// standing in for being able to see every column at once.
class _ColumnTabs extends StatelessWidget {
  const _ColumnTabs({
    required this.columns,
    required this.current,
    required this.onSelect,
  });

  final List<TaskBoardColumn> columns;
  final int current;
  final void Function(int) onSelect;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return SizedBox(
      height: 46,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: Gap.lg),
        itemCount: columns.length,
        separatorBuilder: (_, _) => const SizedBox(width: Gap.sm),
        itemBuilder: (context, i) {
          final col = columns[i];
          final selected = i == current;
          return GestureDetector(
            onTap: () => onSelect(i),
            child: Container(
              alignment: Alignment.center,
              padding: const EdgeInsets.symmetric(horizontal: Gap.md),
              decoration: BoxDecoration(
                color: selected
                    ? col.displayColor.withValues(alpha: 0.15)
                    : scheme.surfaceContainerHighest.withValues(alpha: 0.5),
                borderRadius: BorderRadius.circular(Radii.pill),
                border: Border.all(
                  color: selected ? col.displayColor : Colors.transparent,
                  width: 1.4,
                ),
              ),
              child: Row(
                children: [
                  Container(
                    width: 7,
                    height: 7,
                    decoration: BoxDecoration(
                      color: col.displayColor,
                      shape: BoxShape.circle,
                    ),
                  ),
                  const SizedBox(width: Gap.sm),
                  Text(
                    col.statusName,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                      color: selected
                          ? col.displayColor
                          : scheme.onSurfaceVariant,
                    ),
                  ),
                  const SizedBox(width: 5),
                  Text(
                    '${col.tasks.length}',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: scheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _ColumnView extends ConsumerWidget {
  const _ColumnView({required this.column, required this.allColumns});

  final TaskBoardColumn column;
  final List<TaskBoardColumn> allColumns;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (column.tasks.isEmpty) {
      return EmptyState(
        icon: Icons.inbox_outlined,
        title: 'Nothing in ${column.statusName}',
        message: 'Move a task here from another column.',
      );
    }

    return RefreshIndicator(
      onRefresh: () => ref.read(boardProvider.notifier).refresh(),
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(Gap.lg, Gap.md, Gap.lg, 96),
        itemCount: column.tasks.length,
        separatorBuilder: (_, _) => const SizedBox(height: Gap.sm),
        itemBuilder: (context, i) => _BoardCard(
          task: column.tasks[i],
          column: column,
          allColumns: allColumns,
        ),
      ),
    );
  }
}

/// A card. Tap opens the task; the status chip moves it, which is the board's
/// whole purpose and so gets its own always-visible control rather than being
/// hidden behind a long-press.
class _BoardCard extends ConsumerWidget {
  const _BoardCard({
    required this.task,
    required this.column,
    required this.allColumns,
  });

  final TaskListItem task;
  final TaskBoardColumn column;
  final List<TaskBoardColumn> allColumns;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scheme = Theme.of(context).colorScheme;

    return Card(
      child: InkWell(
        onTap: () => context.push('/tasks/${task.id}'),
        borderRadius: BorderRadius.circular(Radii.card),
        child: Padding(
          padding: const EdgeInsets.all(Gap.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      task.title,
                      maxLines: 3,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 15,
                        height: 1.3,
                        fontWeight: FontWeight.w600,
                        decoration: task.isDone
                            ? TextDecoration.lineThrough
                            : null,
                        color: task.isDone ? scheme.onSurfaceVariant : null,
                      ),
                    ),
                  ),
                  if (task.priority.isNotable) ...[
                    const SizedBox(width: Gap.sm),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 7,
                        vertical: 3,
                      ),
                      decoration: BoxDecoration(
                        color: task.priority.color.withValues(alpha: 0.14),
                        borderRadius: BorderRadius.circular(Radii.pill),
                      ),
                      child: Text(
                        task.priority.wire,
                        style: TextStyle(
                          fontSize: 10.5,
                          fontWeight: FontWeight.w800,
                          color: task.priority.color,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: Gap.md),
              Row(
                children: [
                  _MoveChip(
                    task: task,
                    column: column,
                    allColumns: allColumns,
                  ),
                  const Spacer(),
                  if (task.scheduledOn != null) ...[
                    Icon(
                      Icons.event_rounded,
                      size: 13,
                      color: task.isOverdue
                          ? AppColors.red
                          : scheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: 3),
                    Text(
                      '${task.scheduledOn!.day}/${task.scheduledOn!.month}',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: task.isOverdue
                            ? AppColors.red
                            : scheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                  if (task.hasSubtasks) ...[
                    const SizedBox(width: Gap.md),
                    Icon(
                      Icons.checklist_rounded,
                      size: 13,
                      color: scheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: 3),
                    Text(
                      '${task.subtaskDoneCount}/${task.subtaskCount}',
                      style: TextStyle(
                        fontSize: 12,
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _MoveChip extends ConsumerWidget {
  const _MoveChip({
    required this.task,
    required this.column,
    required this.allColumns,
  });

  final TaskListItem task;
  final TaskBoardColumn column;
  final List<TaskBoardColumn> allColumns;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return GestureDetector(
      onTap: () => _pick(context, ref),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: Gap.sm, vertical: 5),
        decoration: BoxDecoration(
          color: column.displayColor.withValues(alpha: 0.13),
          borderRadius: BorderRadius.circular(Radii.pill),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              column.statusName,
              style: TextStyle(
                fontSize: 11.5,
                fontWeight: FontWeight.w700,
                color: column.displayColor,
              ),
            ),
            const SizedBox(width: 2),
            Icon(
              Icons.unfold_more_rounded,
              size: 13,
              color: column.displayColor,
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _pick(BuildContext context, WidgetRef ref) async {
    final picked = await showModalBottomSheet<String>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.sm),
              child: Text(
                task.title,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
            ),
            const Divider(),
            for (final c in allColumns)
              ListTile(
                leading: Container(
                  width: 12,
                  height: 12,
                  decoration: BoxDecoration(
                    color: c.displayColor,
                    shape: BoxShape.circle,
                  ),
                ),
                title: Text(c.statusName),
                trailing: c.statusId == column.statusId
                    ? const Icon(Icons.check_rounded, color: AppColors.accent)
                    : null,
                onTap: () => Navigator.of(sheetContext).pop(c.statusId),
              ),
          ],
        ),
      ),
    );

    if (picked == null || picked == column.statusId) return;
    HapticFeedback.selectionClick();

    try {
      await ref.read(boardProvider.notifier).moveTo(task.id, picked);
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }
}
