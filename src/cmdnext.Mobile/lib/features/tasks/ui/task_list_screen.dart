import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/task_models.dart';
import '../data/tasks_controller.dart';
import '../data/tasks_repository.dart';
import 'quick_add_sheet.dart';
import 'task_tile.dart';

/// Browse everything: search, filter by tag, include done, unscheduled only.
///
/// Results are grouped by date rather than listed flat — a long list of tasks
/// with dates scattered through it is much harder to read than the same list
/// under Today / Tomorrow / This week headers.
class TaskListScreen extends ConsumerStatefulWidget {
  const TaskListScreen({super.key});

  @override
  ConsumerState<TaskListScreen> createState() => _TaskListScreenState();
}

class _TaskListScreenState extends ConsumerState<TaskListScreen> {
  final _search = TextEditingController();
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  /// Debounced so typing does not fire a request per keystroke.
  void _onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), () {
      ref.read(taskFilterProvider.notifier).setSearch(value.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    final filter = ref.watch(taskFilterProvider);
    final async = ref.watch(taskListProvider);
    final tags = ref.watch(taskTagsProvider).value ?? const [];

    return Scaffold(
      appBar: AppBar(
        title: const Text('All tasks'),
        actions: [
          if (filter.isActive)
            TextButton(
              onPressed: () {
                _search.clear();
                ref.read(taskFilterProvider.notifier).clear();
              },
              child: const Text('Clear'),
            ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.sm),
            child: TextField(
              controller: _search,
              onChanged: _onSearchChanged,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Search tasks',
                prefixIcon: const Icon(Icons.search_rounded),
                isDense: true,
                suffixIcon: _search.text.isEmpty
                    ? null
                    : IconButton(
                        icon: const Icon(Icons.close_rounded, size: 18),
                        onPressed: () {
                          _search.clear();
                          ref.read(taskFilterProvider.notifier).setSearch(null);
                        },
                      ),
              ),
            ),
          ),

          SizedBox(
            height: 40,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: Gap.lg),
              children: [
                _Filter(
                  label: 'Unscheduled',
                  selected: filter.unscheduledOnly,
                  onTap: () => ref
                      .read(taskFilterProvider.notifier)
                      .setUnscheduledOnly(!filter.unscheduledOnly),
                ),
                const SizedBox(width: Gap.sm),
                _Filter(
                  label: 'Show done',
                  selected: filter.includeDone,
                  onTap: () => ref
                      .read(taskFilterProvider.notifier)
                      .setIncludeDone(!filter.includeDone),
                ),
                for (final t in tags) ...[
                  const SizedBox(width: Gap.sm),
                  _Filter(
                    label: t.name,
                    selected: filter.tags.contains(t.name),
                    color: t.displayColor,
                    onTap: () =>
                        ref.read(taskFilterProvider.notifier).toggleTag(t.name),
                  ),
                ],
              ],
            ),
          ),

          const SizedBox(height: Gap.sm),
          Expanded(
            child: async.when(
              loading: () => const LoadingState(),
              error: (e, _) => ErrorState(
                error: e,
                onRetry: () => ref.invalidate(taskListProvider),
              ),
              data: (tasks) => tasks.isEmpty
                  ? EmptyState(
                      icon: Icons.search_off_rounded,
                      title: filter.isActive
                          ? 'Nothing matches'
                          : 'No tasks yet',
                      message: filter.isActive
                          ? 'Try loosening the filters.'
                          : 'Everything you add will show up here.',
                    )
                  : RefreshIndicator(
                      onRefresh: () async => ref.invalidate(taskListProvider),
                      child: _GroupedList(tasks: tasks),
                    ),
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () async {
          final added = await showQuickAddSheet(context);
          if (added) ref.invalidate(taskListProvider);
        },
        tooltip: 'Add task',
        child: const Icon(Icons.add_rounded),
      ),
    );
  }
}

/// Groups tasks under date headings, newest bucket first.
class _GroupedList extends ConsumerWidget {
  const _GroupedList({required this.tasks});

  final List<TaskListItem> tasks;

  /// Buckets a task by how a person thinks about it, not by raw date.
  static String _bucket(TaskListItem t) {
    final d = t.scheduledOn;
    if (d == null) return 'Unscheduled';
    final day = dateOnly(d);
    final diff = day.difference(today()).inDays;
    if (diff < 0) return 'Overdue';
    if (diff == 0) return 'Today';
    if (diff == 1) return 'Tomorrow';
    if (diff <= 7) return 'This week';
    if (diff <= 30) return 'This month';
    return 'Later';
  }

  static const _order = [
    'Overdue',
    'Today',
    'Tomorrow',
    'This week',
    'This month',
    'Later',
    'Unscheduled',
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final groups = <String, List<TaskListItem>>{};
    for (final t in tasks) {
      groups.putIfAbsent(_bucket(t), () => []).add(t);
    }
    final keys = _order.where(groups.containsKey).toList();

    return ListView.builder(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, 96),
      itemCount: keys.length,
      itemBuilder: (context, i) {
        final key = keys[i];
        final items = groups[key]!;
        final overdue = key == 'Overdue';

        return Padding(
          padding: const EdgeInsets.only(bottom: Gap.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.only(left: Gap.xs, bottom: Gap.sm),
                child: Text(
                  '${key.toUpperCase()}  ${items.length}',
                  style: TextStyle(
                    fontSize: 11.5,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 0.7,
                    color: overdue
                        ? AppColors.red
                        : Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
              Card(
                child: Column(
                  children: [
                    for (final (j, task) in items.indexed) ...[
                      if (j > 0)
                        const Divider(indent: Gap.lg + 34 + Gap.md),
                      TaskTile(
                        task: task,
                        showDate: key != 'Today' && key != 'Unscheduled',
                        onTap: () => context.push('/tasks/${task.id}'),
                        onToggle: () async {
                          try {
                            await ref
                                .read(tasksRepositoryProvider)
                                .toggleDone(task.id);
                            ref.invalidate(taskListProvider);
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
          ),
        );
      },
    );
  }
}

class _Filter extends StatelessWidget {
  const _Filter({
    required this.label,
    required this.selected,
    required this.onTap,
    this.color,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final tint = color ?? AppColors.accent;

    return GestureDetector(
      onTap: onTap,
      child: Container(
        alignment: Alignment.center,
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
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? tint : scheme.onSurfaceVariant,
          ),
        ),
      ),
    );
  }
}
