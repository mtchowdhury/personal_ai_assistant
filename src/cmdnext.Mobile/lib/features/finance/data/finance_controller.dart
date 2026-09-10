import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'finance_models.dart';
import 'finance_repository.dart';

/// The month the Money tab is showing. Separate from the data so changing
/// months does not tear down the feature.
class FinanceMonthController extends Notifier<DateTime> {
  @override
  DateTime build() {
    final now = DateTime.now();
    return DateTime(now.year, now.month);
  }

  void set(DateTime month) => state = DateTime(month.year, month.month);
  void next() => state = DateTime(state.year, state.month + 1);
  void previous() => state = DateTime(state.year, state.month - 1);

  bool get isCurrentMonth {
    final now = DateTime.now();
    return state.year == now.year && state.month == now.month;
  }
}

final financeMonthProvider =
    NotifierProvider<FinanceMonthController, DateTime>(
      FinanceMonthController.new,
    );

class FinanceDashboardController extends AsyncNotifier<FinanceDashboard> {
  @override
  Future<FinanceDashboard> build() {
    final month = ref.watch(financeMonthProvider);
    return ref
        .watch(financeRepositoryProvider)
        .dashboard(year: month.year, month: month.month);
  }

  Future<void> refresh() async {
    final month = ref.read(financeMonthProvider);
    state = AsyncData(
      await ref
          .read(financeRepositoryProvider)
          .dashboard(year: month.year, month: month.month),
    );
  }
}

final financeDashboardProvider =
    AsyncNotifierProvider<FinanceDashboardController, FinanceDashboard>(
      FinanceDashboardController.new,
    );

/// Expenses for the selected month. Auto-disposed — the list can be long and
/// is cheap to refetch.
final expenseListProvider = FutureProvider.autoDispose<List<ExpenseListItem>>((
  ref,
) async {
  final month = ref.watch(financeMonthProvider);
  final search = ref.watch(expenseSearchProvider);
  return ref.watch(financeRepositoryProvider).expenses(
    year: month.year,
    month: month.month,
    search: search,
  );
});

class ExpenseSearchController extends Notifier<String?> {
  @override
  String? build() => null;

  void set(String? value) =>
      state = (value == null || value.trim().isEmpty) ? null : value.trim();
}

final expenseSearchProvider =
    NotifierProvider<ExpenseSearchController, String?>(
      ExpenseSearchController.new,
    );

/// Categories and budgets change rarely, so they are held for the session.
final expenseCategoriesProvider = FutureProvider<List<ExpenseCategory>>(
  (ref) => ref.watch(financeRepositoryProvider).categories(),
);

final budgetsProvider = FutureProvider<List<Budget>>(
  (ref) => ref.watch(financeRepositoryProvider).budgets(),
);

/// Months that actually have expenses, for the month picker.
final expensePeriodsProvider = FutureProvider<List<DateTime>>(
  (ref) => ref.watch(financeRepositoryProvider).periods(),
);

/// One expense with its line items.
class ExpenseDetailController extends AsyncNotifier<ExpenseDetail> {
  ExpenseDetailController(this.expenseId);

  final String expenseId;

  @override
  Future<ExpenseDetail> build() =>
      ref.watch(financeRepositoryProvider).expense(expenseId);

  void _invalidateLists() {
    ref.invalidate(financeDashboardProvider);
    ref.invalidate(expenseListProvider);
  }

  Future<void> refresh() async {
    state = AsyncData(
      await ref.read(financeRepositoryProvider).expense(expenseId),
    );
  }

  Future<void> save({
    String? shopName,
    DateTime? purchasedOn,
    Money? totalAmount,
    String? categoryId,
    String? paymentMethod,
    String? notes,
  }) async {
    final updated = await ref.read(financeRepositoryProvider).updateExpense(
      expenseId,
      shopName: shopName,
      purchasedOn: purchasedOn,
      totalAmount: totalAmount,
      categoryId: categoryId,
      paymentMethod: paymentMethod,
      notes: notes,
    );
    state = AsyncData(updated);
    _invalidateLists();
  }

  Future<void> attachReceipt(String filePath, String fileName) async {
    await ref.read(financeRepositoryProvider).uploadReceipt(
      expenseId: expenseId,
      filePath: filePath,
      fileName: fileName,
    );
    await refresh();
    ref.invalidate(receiptProvider(expenseId));
    _invalidateLists();
  }

  Future<void> deleteItem(String itemId) async {
    await ref.read(financeRepositoryProvider).deleteItem(itemId);
    await refresh();
    _invalidateLists();
  }

  Future<void> delete() async {
    await ref.read(financeRepositoryProvider).deleteExpense(expenseId);
    _invalidateLists();
  }
}

final expenseDetailProvider = AsyncNotifierProvider.autoDispose
    .family<ExpenseDetailController, ExpenseDetail, String>(
      ExpenseDetailController.new,
    );

/// Receipt bytes, fetched through Dio because the endpoint needs the bearer
/// token and `Image.network` cannot send one.
final receiptProvider = FutureProvider.autoDispose.family(
  (ref, String expenseId) =>
      ref.watch(financeRepositoryProvider).receiptBytes(expenseId),
);
