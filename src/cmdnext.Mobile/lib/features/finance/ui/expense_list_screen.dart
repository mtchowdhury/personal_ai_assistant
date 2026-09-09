import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/finance_controller.dart';
import '../data/finance_models.dart';
import 'add_expense_sheet.dart';

/// Every expense in the selected month, grouped by day with a daily subtotal —
/// the shape a person actually reads a statement in.
class ExpenseListScreen extends ConsumerStatefulWidget {
  const ExpenseListScreen({super.key});

  @override
  ConsumerState<ExpenseListScreen> createState() => _ExpenseListScreenState();
}

class _ExpenseListScreenState extends ConsumerState<ExpenseListScreen> {
  final _search = TextEditingController();
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), () {
      ref.read(expenseSearchProvider.notifier).set(value);
    });
  }

  @override
  Widget build(BuildContext context) {
    final month = ref.watch(financeMonthProvider);
    final async = ref.watch(expenseListProvider);

    return Scaffold(
      appBar: AppBar(title: Text(DateFormat('MMMM y').format(month))),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.sm),
            child: TextField(
              controller: _search,
              onChanged: _onSearchChanged,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Search expenses',
                prefixIcon: const Icon(Icons.search_rounded),
                isDense: true,
                suffixIcon: _search.text.isEmpty
                    ? null
                    : IconButton(
                        icon: const Icon(Icons.close_rounded, size: 18),
                        onPressed: () {
                          _search.clear();
                          ref.read(expenseSearchProvider.notifier).set(null);
                        },
                      ),
              ),
            ),
          ),
          Expanded(
            child: async.when(
              loading: () => const LoadingState(),
              error: (e, _) => ErrorState(
                error: e,
                onRetry: () => ref.invalidate(expenseListProvider),
              ),
              data: (list) => list.isEmpty
                  ? const EmptyState(
                      icon: Icons.receipt_long_outlined,
                      title: 'Nothing here',
                      message: 'No expenses match this month.',
                    )
                  : RefreshIndicator(
                      onRefresh: () async =>
                          ref.invalidate(expenseListProvider),
                      child: _ByDay(expenses: list),
                    ),
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () async {
          final added = await showAddExpenseSheet(context);
          if (added) ref.invalidate(expenseListProvider);
        },
        tooltip: 'Add expense',
        child: const Icon(Icons.add_rounded),
      ),
    );
  }
}

class _ByDay extends StatelessWidget {
  const _ByDay({required this.expenses});

  final List<ExpenseListItem> expenses;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    // Group by calendar day, preserving the server's newest-first ordering.
    final days = <DateTime, List<ExpenseListItem>>{};
    for (final e in expenses) {
      final d = DateTime(
        e.purchasedOn.year,
        e.purchasedOn.month,
        e.purchasedOn.day,
      );
      days.putIfAbsent(d, () => []).add(e);
    }
    final keys = days.keys.toList()..sort((a, b) => b.compareTo(a));

    return ListView.builder(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, 96),
      itemCount: keys.length,
      itemBuilder: (context, i) {
        final day = keys[i];
        final items = days[day]!;
        final currency = items.first.currency;
        final subtotal = items.fold<Money>(0, (s, e) => s + e.totalAmount);

        return Padding(
          padding: const EdgeInsets.only(bottom: Gap.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.only(
                  left: Gap.xs,
                  right: Gap.xs,
                  bottom: Gap.sm,
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      DateFormat('EEE d MMM').format(day).toUpperCase(),
                      style: TextStyle(
                        fontSize: 11.5,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 0.7,
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                    Text(
                      formatMoney(subtotal, currency),
                      style: TextStyle(
                        fontSize: 11.5,
                        fontWeight: FontWeight.w700,
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
              Card(
                child: Column(
                  children: [
                    for (final (j, e) in items.indexed) ...[
                      if (j > 0) const Divider(indent: Gap.md),
                      InkWell(
                        onTap: () => context.push('/finance/expenses/${e.id}'),
                        child: Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: Gap.md,
                            vertical: Gap.md,
                          ),
                          child: Row(
                            children: [
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      e.displayName,
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(
                                        fontSize: 15,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                    if (e.categoryName != null ||
                                        e.itemCount > 0) ...[
                                      const SizedBox(height: 2),
                                      Row(
                                        children: [
                                          if (e.hasImage) ...[
                                            Icon(
                                              Icons.receipt_long_rounded,
                                              size: 12,
                                              color: scheme.onSurfaceVariant,
                                            ),
                                            const SizedBox(width: 4),
                                          ],
                                          Flexible(
                                            child: Text(
                                              [
                                                if (e.categoryName != null)
                                                  e.categoryName!,
                                                if (e.itemCount > 0)
                                                  '${e.itemCount} items',
                                              ].join(' · '),
                                              maxLines: 1,
                                              overflow: TextOverflow.ellipsis,
                                              style: TextStyle(
                                                fontSize: 12,
                                                color: scheme.onSurfaceVariant,
                                              ),
                                            ),
                                          ),
                                        ],
                                      ),
                                    ],
                                  ],
                                ),
                              ),
                              const SizedBox(width: Gap.sm),
                              Text(
                                formatMoney(e.totalAmount, e.currency),
                                style: const TextStyle(
                                  fontSize: 15,
                                  fontWeight: FontWeight.w700,
                                  letterSpacing: -0.3,
                                ),
                              ),
                            ],
                          ),
                        ),
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
