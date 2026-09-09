import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import 'finance_models.dart';

/// Talks to `/finance`.
class FinanceRepository {
  const FinanceRepository(this._api);

  final ApiClient _api;

  // ---- Dashboard ----

  Future<FinanceDashboard> dashboard({int? year, int? month}) async =>
      FinanceDashboard.fromJson(
        await _api.getObject(
          'finance/dashboard',
          query: {
            if (year != null) 'year': year,
            if (month != null) 'month': month,
          },
        ),
      );

  // ---- Categories ----

  Future<List<ExpenseCategory>> categories() async =>
      (await _api.getList('finance/categories'))
          .map(ExpenseCategory.fromJson)
          .toList(growable: false);

  // ---- Expenses ----

  Future<List<ExpenseListItem>> expenses({
    List<String>? categoryIds,
    int take = 100,
    int? year,
    int? month,
    String? search,
  }) async => (await _api.getList(
    'finance/expenses',
    query: {
      'take': take,
      if (categoryIds != null && categoryIds.isNotEmpty)
        'categoryId': categoryIds,
      if (year != null) 'year': year,
      if (month != null) 'month': month,
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    },
  )).map(ExpenseListItem.fromJson).toList(growable: false);

  Future<ExpenseDetail> expense(String id) async =>
      ExpenseDetail.fromJson(await _api.getObject('finance/expenses/$id'));

  /// Months that actually contain expenses, newest first — drives the month
  /// picker, so it never offers an empty month.
  Future<List<DateTime>> periods() async =>
      (await _api.getList('finance/expenses/periods'))
          .map(
            (j) => DateTime(
              (j['year'] as num?)?.toInt() ?? 0,
              (j['month'] as num?)?.toInt() ?? 1,
            ),
          )
          .where((d) => d.year > 0)
          .toList(growable: false);

  Future<ExpenseDetail> createExpense({
    String? shopName,
    DateTime? purchasedOn,
    Money? totalAmount,
    String? currency,
    String? categoryId,
    String? categoryName,
    String? paymentMethod,
    String? notes,
    List<ExpenseItem> items = const [],
  }) async {
    final json = await _api.post<Map<String, dynamic>>(
      'finance/expenses',
      body: {
        if (shopName != null && shopName.trim().isNotEmpty)
          'shopName': shopName.trim(),
        // A real instant here, unlike a task's date — the API keeps the time.
        if (purchasedOn != null)
          'purchasedOn': purchasedOn.toUtc().toIso8601String(),
        if (totalAmount != null) 'totalAmount': totalAmount,
        if (currency != null) 'currency': currency,
        if (categoryId != null) 'categoryId': categoryId,
        if (categoryName != null) 'categoryName': categoryName,
        if (paymentMethod != null) 'paymentMethod': paymentMethod,
        if (notes != null && notes.trim().isNotEmpty) 'notes': notes.trim(),
        'source': 'mobile',
        'items': [for (final i in items) i.toCreateJson()],
      },
    );
    return ExpenseDetail.fromJson(json);
  }

  Future<ExpenseDetail> updateExpense(
    String id, {
    String? shopName,
    DateTime? purchasedOn,
    Money? totalAmount,
    String? currency,
    String? categoryId,
    String? paymentMethod,
    String? notes,
  }) async {
    final json = await _api.put<Map<String, dynamic>>(
      'finance/expenses/$id',
      body: {
        if (shopName != null) 'shopName': shopName.trim(),
        if (purchasedOn != null)
          'purchasedOn': purchasedOn.toUtc().toIso8601String(),
        if (totalAmount != null) 'totalAmount': totalAmount,
        if (currency != null) 'currency': currency,
        if (categoryId != null) 'categoryId': categoryId,
        if (paymentMethod != null) 'paymentMethod': paymentMethod,
        if (notes != null) 'notes': notes,
      },
    );
    return ExpenseDetail.fromJson(json);
  }

  Future<void> deleteExpense(String id) =>
      _api.delete<void>('finance/expenses/$id');

  Future<void> deleteItem(String itemId) =>
      _api.delete<void>('finance/items/$itemId');

  // ---- Budgets ----

  Future<List<Budget>> budgets() async =>
      (await _api.getList('finance/budgets'))
          .map(Budget.fromJson)
          .toList(growable: false);

  Future<void> setBudget({
    required String categoryId,
    required Money monthlyLimit,
    String? currency,
  }) => _api.put<void>(
    'finance/budgets',
    body: {
      'categoryId': categoryId,
      'monthlyLimit': monthlyLimit,
      if (currency != null) 'currency': currency,
    },
  );

  Future<void> deleteBudget(String id) =>
      _api.delete<void>('finance/budgets/$id');

  // ---- Receipt image ----

  /// Uploads a receipt photo. The server caps this at 10 MB and only accepts
  /// the extensions in its allowlist, so the filename must carry a real one.
  Future<void> uploadReceipt({
    required String expenseId,
    required String filePath,
    required String fileName,
  }) async {
    final form = FormData.fromMap({
      'file': await MultipartFile.fromFile(filePath, filename: fileName),
    });
    try {
      await _api.raw.post<dynamic>(
        'finance/expenses/$expenseId/image',
        data: form,
      );
    } on DioException catch (e) {
      throw ApiException.from(e);
    }
  }

  /// Fetches a receipt as bytes.
  ///
  /// The endpoint requires the bearer token, so `Image.network` cannot load it
  /// — it has no way to attach the header. Going through Dio keeps the
  /// interceptor in play.
  Future<Uint8List?> receiptBytes(String expenseId) async {
    try {
      final response = await _api.raw.get<List<int>>(
        'finance/expenses/$expenseId/image',
        options: Options(responseType: ResponseType.bytes),
      );
      final data = response.data;
      return data == null ? null : Uint8List.fromList(data);
    } on DioException catch (e) {
      // A missing image is a normal state, not an error worth surfacing.
      if (e.response?.statusCode == 404) return null;
      throw ApiException.from(e);
    }
  }
}

final financeRepositoryProvider = Provider<FinanceRepository>(
  (ref) => FinanceRepository(ref.watch(apiClientProvider)),
);
