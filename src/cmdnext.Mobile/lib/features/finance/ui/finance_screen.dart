import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/finance_controller.dart';
import '../data/finance_models.dart';
import 'add_expense_sheet.dart';

/// The Money tab.
///
/// The web finance page is a table of expenses with a chart above it. On a
/// phone the question is simpler and more urgent — "how much have I spent this
/// month, and am I over?" — so the screen leads with one number and a budget
/// ring, then category bars, then the recent expenses. Everything else is a
/// tap away.
class FinanceScreen extends ConsumerWidget {
  const FinanceScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(financeDashboardProvider);

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () => ref.read(financeDashboardProvider.notifier).refresh(),
        child: async.when(
          loading: () => const LoadingState(),
          error: (e, _) => CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              SliverFillRemaining(
                hasScrollBody: false,
                child: ErrorState(
                  error: e,
                  onRetry: () => ref.invalidate(financeDashboardProvider),
                ),
              ),
            ],
          ),
          data: (d) => _Body(dashboard: d),
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => showAddExpenseSheet(context),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Expense'),
      ),
    );
  }
}

class _Body extends ConsumerWidget {
  const _Body({required this.dashboard});

  final FinanceDashboard dashboard;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final d = dashboard;
    final expenses = ref.watch(expenseListProvider);

    return CustomScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      slivers: [
        SliverAppBar.large(
          title: const Text('Money'),
          actions: [
            IconButton(
              tooltip: 'Budgets',
              icon: const Icon(Icons.tune_rounded),
              onPressed: () => context.push('/finance/budgets'),
            ),
          ],
        ),

        SliverToBoxAdapter(child: _MonthSwitcher(month: d.monthDate)),
        SliverToBoxAdapter(child: _SpendCard(dashboard: d)),

        if (d.spentCategories.isNotEmpty)
          SliverToBoxAdapter(child: _Categories(dashboard: d)),

        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(Gap.lg + Gap.xs, 0, Gap.lg, Gap.sm),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'RECENT',
                  style: TextStyle(
                    fontSize: 11.5,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 0.7,
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
                if (d.monthExpenseCount > 0)
                  TextButton(
                    onPressed: () => context.push('/finance/expenses'),
                    child: Text('All ${d.monthExpenseCount}'),
                  ),
              ],
            ),
          ),
        ),

        expenses.when(
          loading: () => const SliverToBoxAdapter(
            child: Padding(
              padding: EdgeInsets.all(Gap.xl),
              child: LoadingState(),
            ),
          ),
          error: (e, _) => SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.all(Gap.lg),
              child: ErrorState(
                error: e,
                onRetry: () => ref.invalidate(expenseListProvider),
              ),
            ),
          ),
          data: (list) => list.isEmpty
              ? SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: Gap.xl),
                    child: EmptyState(
                      icon: Icons.receipt_long_outlined,
                      title: 'Nothing logged',
                      message:
                          'No expenses in '
                          '${DateFormat('MMMM').format(d.monthDate)} yet.',
                    ),
                  ),
                )
              : SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: Gap.lg),
                    child: Card(
                      child: Column(
                        children: [
                          for (final (i, e) in list.take(8).indexed) ...[
                            if (i > 0) const Divider(indent: 60),
                            _ExpenseRow(expense: e),
                          ],
                        ],
                      ),
                    ),
                  ),
                ),
        ),

        const SliverToBoxAdapter(child: SizedBox(height: 96)),
      ],
    );
  }
}

/// Month stepper. Forward is disabled past the current month — there is
/// nothing to see in the future, and letting it run makes the header lie.
class _MonthSwitcher extends ConsumerWidget {
  const _MonthSwitcher({required this.month});

  final DateTime month;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final now = DateTime.now();
    final atCurrent = month.year == now.year && month.month == now.month;

    return Padding(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.sm),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          IconButton(
            onPressed: () =>
                ref.read(financeMonthProvider.notifier).previous(),
            icon: const Icon(Icons.chevron_left_rounded),
            tooltip: 'Previous month',
          ),
          Expanded(
            child: Text(
              DateFormat('MMMM y').format(month),
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 15,
                fontWeight: FontWeight.w700,
                letterSpacing: -0.2,
              ),
            ),
          ),
          IconButton(
            onPressed: atCurrent
                ? null
                : () => ref.read(financeMonthProvider.notifier).next(),
            icon: const Icon(Icons.chevron_right_rounded),
            tooltip: 'Next month',
          ),
        ],
      ),
    );
  }
}

/// The headline: total spent, and the budget ring if a budget exists.
class _SpendCard extends StatelessWidget {
  const _SpendCard({required this.dashboard});

  final FinanceDashboard dashboard;

  @override
  Widget build(BuildContext context) {
    final d = dashboard;
    final scheme = Theme.of(context).colorScheme;
    final over = d.hasBudget && d.remaining < 0;

    return Padding(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.lg),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(Gap.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Spent',
                          style: TextStyle(
                            fontSize: 13,
                            color: scheme.onSurfaceVariant,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          formatMoney(d.monthTotal, d.currency),
                          style: const TextStyle(
                            fontSize: 32,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -1.2,
                            height: 1.1,
                          ),
                        ),
                        const SizedBox(height: Gap.xs),
                        Text(
                          d.monthExpenseCount == 0
                              ? 'No expenses yet'
                              : '${d.monthExpenseCount} '
                                    '${d.monthExpenseCount == 1 ? 'expense' : 'expenses'}',
                          style: TextStyle(
                            fontSize: 13,
                            color: scheme.onSurfaceVariant,
                          ),
                        ),
                      ],
                    ),
                  ),
                  if (d.hasBudget)
                    _BudgetRing(
                      progress: d.budgetProgress,
                      over: over,
                      label: '${(d.rawBudgetProgress * 100).round()}%',
                    ),
                ],
              ),

              if (d.hasBudget) ...[
                const SizedBox(height: Gap.lg),
                const Divider(),
                const SizedBox(height: Gap.md),
                Row(
                  children: [
                    Icon(
                      over
                          ? Icons.warning_amber_rounded
                          : Icons.savings_outlined,
                      size: 17,
                      color: over ? AppColors.red : AppColors.green,
                    ),
                    const SizedBox(width: Gap.sm),
                    Text(
                      over
                          ? '${formatMoney(d.remaining.abs(), d.currency)} over budget'
                          : '${formatMoney(d.remaining, d.currency)} left',
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: over ? AppColors.red : AppColors.green,
                      ),
                    ),
                    const Spacer(),
                    Text(
                      'of ${formatMoney(d.totalBudget, d.currency)}',
                      style: TextStyle(
                        fontSize: 13,
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _BudgetRing extends StatelessWidget {
  const _BudgetRing({
    required this.progress,
    required this.over,
    required this.label,
  });

  final double progress;
  final bool over;
  final String label;

  @override
  Widget build(BuildContext context) => SizedBox(
    width: 72,
    height: 72,
    child: Stack(
      alignment: Alignment.center,
      children: [
        SizedBox.expand(
          child: TweenAnimationBuilder<double>(
            tween: Tween(begin: 0, end: progress),
            duration: const Duration(milliseconds: 600),
            curve: Curves.easeOutCubic,
            builder: (context, value, _) => CircularProgressIndicator(
              value: value,
              strokeWidth: 7,
              strokeCap: StrokeCap.round,
              backgroundColor:
                  Theme.of(context).colorScheme.surfaceContainerHighest,
              valueColor: AlwaysStoppedAnimation(
                over
                    ? AppColors.red
                    : progress >= 0.85
                    ? AppColors.amber
                    : AppColors.green,
              ),
            ),
          ),
        ),
        Text(
          label,
          style: const TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w800,
            letterSpacing: -0.4,
          ),
        ),
      ],
    ),
  );
}

/// Category bars. A bar chart rather than a pie: comparing lengths against a
/// shared baseline is far easier than comparing angles, and the budget line
/// has somewhere to sit.
class _Categories extends StatelessWidget {
  const _Categories({required this.dashboard});

  final FinanceDashboard dashboard;

  @override
  Widget build(BuildContext context) {
    final d = dashboard;
    final cats = d.spentCategories;
    // Scale bars against the largest spend so the biggest fills the row.
    final max = cats.first.spent;

    return Padding(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.only(left: Gap.xs, bottom: Gap.sm),
            child: Text(
              'BY CATEGORY',
              style: TextStyle(
                fontSize: 11.5,
                fontWeight: FontWeight.w800,
                letterSpacing: 0.7,
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
          ),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(Gap.lg),
              child: Column(
                children: [
                  for (final (i, c) in cats.take(6).indexed) ...[
                    if (i > 0) const SizedBox(height: Gap.md),
                    _CategoryBar(
                      spend: c,
                      currency: d.currency,
                      fraction: max <= 0 ? 0 : c.spent / max,
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _CategoryBar extends StatelessWidget {
  const _CategoryBar({
    required this.spend,
    required this.currency,
    required this.fraction,
  });

  final CategorySpend spend;
  final String currency;
  final double fraction;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                spend.categoryName,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: 13.5,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            if (spend.overBudget) ...[
              const Icon(
                Icons.warning_amber_rounded,
                size: 14,
                color: AppColors.red,
              ),
              const SizedBox(width: 4),
            ],
            Text(
              formatMoney(spend.spent, currency),
              style: TextStyle(
                fontSize: 13.5,
                fontWeight: FontWeight.w700,
                color: spend.overBudget ? AppColors.red : null,
              ),
            ),
          ],
        ),
        const SizedBox(height: 5),
        ClipRRect(
          borderRadius: BorderRadius.circular(Radii.pill),
          child: Stack(
            children: [
              Container(
                height: 7,
                color: scheme.surfaceContainerHighest,
              ),
              // Relative-spend bar, so categories compare against each other.
              FractionallySizedBox(
                widthFactor: fraction.clamp(0.0, 1.0),
                child: Container(
                  height: 7,
                  decoration: BoxDecoration(
                    color: spend.hasBudget ? spend.color : AppColors.accent,
                    borderRadius: BorderRadius.circular(Radii.pill),
                  ),
                ),
              ),
            ],
          ),
        ),
        if (spend.hasBudget) ...[
          const SizedBox(height: 3),
          Text(
            spend.overBudget
                ? 'Over by ${formatMoney(spend.spent - spend.budgetLimit!, currency)}'
                : '${formatMoney(spend.budgetLimit! - spend.spent, currency)} left '
                      'of ${formatMoney(spend.budgetLimit!, currency)}',
            style: TextStyle(
              fontSize: 11.5,
              color: spend.overBudget ? AppColors.red : scheme.onSurfaceVariant,
            ),
          ),
        ],
      ],
    );
  }
}

class _ExpenseRow extends StatelessWidget {
  const _ExpenseRow({required this.expense});

  final ExpenseListItem expense;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return InkWell(
      onTap: () => context.push('/finance/expenses/${expense.id}'),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: Gap.lg,
          vertical: Gap.md,
        ),
        child: Row(
          children: [
            Container(
              width: 34,
              height: 34,
              decoration: BoxDecoration(
                color: AppColors.accent.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(10),
              ),
              alignment: Alignment.center,
              child: Icon(
                expense.hasImage
                    ? Icons.receipt_long_rounded
                    : Icons.shopping_bag_outlined,
                size: 17,
                color: AppColors.accent,
              ),
            ),
            const SizedBox(width: Gap.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    expense.displayName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    [
                      DateFormat('d MMM').format(expense.purchasedOn),
                      if (expense.categoryName != null) expense.categoryName!,
                      if (expense.itemCount > 0)
                        '${expense.itemCount} '
                            '${expense.itemCount == 1 ? 'item' : 'items'}',
                    ].join(' · '),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 12,
                      color: scheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: Gap.sm),
            Text(
              formatMoney(expense.totalAmount, expense.currency),
              style: const TextStyle(
                fontSize: 15,
                fontWeight: FontWeight.w700,
                letterSpacing: -0.3,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
