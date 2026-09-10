import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/finance_controller.dart';
import '../data/finance_models.dart';
import '../data/finance_repository.dart';

/// Monthly budget per category.
///
/// Every category is listed, budgeted or not, so setting one is a tap rather
/// than a hunt through an "add budget" flow.
class BudgetsScreen extends ConsumerWidget {
  const BudgetsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final categories = ref.watch(expenseCategoriesProvider);
    final budgets = ref.watch(budgetsProvider);
    final dashboard = ref.watch(financeDashboardProvider).value;

    return Scaffold(
      appBar: AppBar(title: const Text('Budgets')),
      body: categories.when(
        loading: () => const LoadingState(),
        error: (e, _) => ErrorState(
          error: e,
          onRetry: () => ref.invalidate(expenseCategoriesProvider),
        ),
        data: (cats) {
          if (cats.isEmpty) {
            return const EmptyState(
              icon: Icons.sell_outlined,
              title: 'No categories',
              message: 'Add a category in the web app first.',
            );
          }

          final byCategory = {
            for (final b in budgets.value ?? const <Budget>[]) b.categoryId: b,
          };
          // Spend comes from the dashboard, so a budget can be judged against
          // what has actually been spent this month.
          final spendByCategory = {
            for (final c in dashboard?.categories ?? const <CategorySpend>[])
              if (c.categoryId != null) c.categoryId!: c,
          };
          final currency = dashboard?.currency ?? 'EUR';

          return ListView(
            padding: const EdgeInsets.fromLTRB(Gap.lg, Gap.sm, Gap.lg, Gap.xxl),
            children: [
              Card(
                child: Column(
                  children: [
                    for (final (i, c) in cats.indexed) ...[
                      if (i > 0) const Divider(indent: 52),
                      _BudgetRow(
                        category: c,
                        budget: byCategory[c.id],
                        spend: spendByCategory[c.id],
                        currency: currency,
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: Gap.lg),
              Text(
                'Budgets are monthly and reset on the 1st.',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 12.5,
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _BudgetRow extends ConsumerWidget {
  const _BudgetRow({
    required this.category,
    required this.budget,
    required this.spend,
    required this.currency,
  });

  final ExpenseCategory category;
  final Budget? budget;
  final CategorySpend? spend;
  final String currency;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scheme = Theme.of(context).colorScheme;
    final limit = budget?.monthlyLimit;
    final spent = spend?.spent ?? 0;
    final over = limit != null && limit > 0 && spent > limit;

    return InkWell(
      onTap: () => _edit(context, ref),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: Gap.md,
          vertical: Gap.md,
        ),
        child: Row(
          children: [
            Icon(category.icon, size: 20, color: scheme.onSurfaceVariant),
            const SizedBox(width: Gap.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    category.name,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  if (limit != null && limit > 0) ...[
                    const SizedBox(height: 5),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(Radii.pill),
                      child: LinearProgressIndicator(
                        value: (spent / limit).clamp(0.0, 1.0),
                        minHeight: 5,
                        backgroundColor: scheme.surfaceContainerHighest,
                        valueColor: AlwaysStoppedAnimation(
                          over
                              ? AppColors.red
                              : spent / limit >= 0.85
                              ? AppColors.amber
                              : AppColors.green,
                        ),
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '${formatMoney(spent, currency)} of '
                      '${formatMoney(limit, currency)}',
                      style: TextStyle(
                        fontSize: 11.5,
                        color: over ? AppColors.red : scheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: Gap.sm),
            Text(
              limit == null || limit == 0
                  ? 'Set'
                  : formatMoney(limit, currency),
              style: TextStyle(
                fontSize: 14.5,
                fontWeight: FontWeight.w700,
                color: limit == null || limit == 0
                    ? AppColors.accent
                    : scheme.onSurface,
              ),
            ),
            Icon(
              Icons.chevron_right_rounded,
              size: 19,
              color: scheme.onSurfaceVariant,
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _edit(BuildContext context, WidgetRef ref) async {
    final controller = TextEditingController(
      text: budget == null || budget!.monthlyLimit == 0
          ? ''
          : budget!.monthlyLimit.toStringAsFixed(2),
    );

    final result = await showModalBottomSheet<({bool clear, Money? value})>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => Padding(
        padding: EdgeInsets.only(
          left: Gap.xl,
          right: Gap.xl,
          top: Gap.sm,
          bottom: MediaQuery.viewInsetsOf(sheetContext).bottom + Gap.xl,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              '${category.name} budget',
              style: Theme.of(sheetContext).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: Gap.lg),
            TextField(
              controller: controller,
              autofocus: true,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]')),
              ],
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 32,
                fontWeight: FontWeight.w800,
                letterSpacing: -1,
              ),
              decoration: const InputDecoration(
                hintText: '0.00',
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                filled: false,
              ),
              onSubmitted: (v) => Navigator.of(sheetContext).pop((
                clear: false,
                value: double.tryParse(v.trim().replaceAll(',', '.')),
              )),
            ),
            const SizedBox(height: Gap.lg),
            FilledButton(
              onPressed: () => Navigator.of(sheetContext).pop((
                clear: false,
                value: double.tryParse(
                  controller.text.trim().replaceAll(',', '.'),
                ),
              )),
              child: const Text('Save'),
            ),
            if (budget != null)
              TextButton(
                onPressed: () => Navigator.of(
                  sheetContext,
                ).pop((clear: true, value: null)),
                style: TextButton.styleFrom(foregroundColor: AppColors.red),
                child: const Text('Remove budget'),
              ),
          ],
        ),
      ),
    );
    controller.dispose();

    if (result == null) return;
    final repo = ref.read(financeRepositoryProvider);

    try {
      if (result.clear) {
        if (budget != null) await repo.deleteBudget(budget!.id);
      } else {
        final v = result.value;
        if (v == null || v <= 0) return;
        await repo.setBudget(
          categoryId: category.id,
          monthlyLimit: v,
          currency: currency,
        );
      }
      ref.invalidate(budgetsProvider);
      ref.invalidate(financeDashboardProvider);
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }
}
