import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../tasks/data/task_models.dart' show parseApiInstant;

/// Money, kept as a `double` because that is what the API sends and the sums
/// here are personal-scale. Formatting always goes through [formatMoney] so
/// rounding is consistent across every screen.
typedef Money = double;

Money parseMoney(Object? v) => switch (v) {
  num n => n.toDouble(),
  String s => double.tryParse(s) ?? 0,
  _ => 0,
};

/// `purchasedOn` is a true instant here — unlike a task's `scheduledOn`,
/// `FinanceService` calls `.ToUniversalTime()` and keeps the time of day — so
/// converting to local time is correct.
DateTime? parsePurchaseDate(Object? v) => parseApiInstant(v);

/// Formats an amount in its own currency. The API stores a currency per
/// expense, so a single hardcoded symbol would be wrong the moment a trip
/// abroad is logged.
String formatMoney(Money amount, String currency, {bool compact = false}) {
  final format = NumberFormat.simpleCurrency(name: currency);
  if (compact && amount.abs() >= 1000) {
    return '${format.currencySymbol}${(amount / 1000).toStringAsFixed(1)}k';
  }
  return format.format(amount);
}

class ExpenseCategory {
  const ExpenseCategory({
    required this.id,
    required this.name,
    required this.showPartyField,
    required this.isProtected,
    this.partyLabel,
  });

  final String id;
  final String name;

  /// What the counterparty is called for this category — "Shop" for groceries,
  /// "Doctor/Hospital" for health. Drives the label on the expense form.
  final String? partyLabel;
  final bool showPartyField;
  final bool isProtected;

  /// Categories are user-defined, so the icon is derived from the name rather
  /// than stored. Falls back to a generic tag for anything unrecognised.
  IconData get icon => switch (name.toLowerCase()) {
    'groceries' => Icons.local_grocery_store_outlined,
    'dining' => Icons.restaurant_outlined,
    'transport' => Icons.directions_bus_outlined,
    'household' => Icons.home_outlined,
    'health' => Icons.medical_services_outlined,
    'other' => Icons.more_horiz_rounded,
    _ => Icons.sell_outlined,
  };

  factory ExpenseCategory.fromJson(Map<String, dynamic> j) => ExpenseCategory(
    id: (j['id'] ?? '').toString(),
    name: (j['name'] ?? '').toString(),
    partyLabel: j['partyLabel'] as String?,
    showPartyField: j['showPartyField'] == true,
    isProtected: j['isProtected'] == true,
  );
}

/// A line item on a receipt.
class ExpenseItem {
  const ExpenseItem({
    required this.id,
    required this.rawName,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
    this.canonicalName,
    this.categoryId,
    this.categoryName,
    this.notes,
  });

  final String id;
  final String rawName;

  /// The normalised name the AI groups by — "Milk 1L" and "milk" collapse to
  /// one canonical entry for trend analysis.
  final String? canonicalName;
  final String? categoryId;
  final String? categoryName;
  final double quantity;
  final Money unitPrice;
  final Money lineTotal;
  final String? notes;

  String get displayName => canonicalName?.trim().isNotEmpty == true
      ? canonicalName!
      : rawName;

  factory ExpenseItem.fromJson(Map<String, dynamic> j) => ExpenseItem(
    id: (j['id'] ?? '').toString(),
    rawName: (j['rawName'] ?? '').toString(),
    canonicalName: j['canonicalName'] as String?,
    categoryId: j['categoryId']?.toString(),
    categoryName: j['categoryName'] as String?,
    quantity: parseMoney(j['quantity']),
    unitPrice: parseMoney(j['unitPrice']),
    lineTotal: parseMoney(j['lineTotal']),
    notes: j['notes'] as String?,
  );

  Map<String, dynamic> toCreateJson() => {
    'rawName': rawName,
    if (canonicalName != null) 'canonicalName': canonicalName,
    if (categoryId != null) 'categoryId': categoryId,
    'quantity': quantity,
    'unitPrice': unitPrice,
    'lineTotal': lineTotal,
    if (notes != null) 'notes': notes,
  };
}

/// An expense as it appears in a list.
class ExpenseListItem {
  const ExpenseListItem({
    required this.id,
    required this.purchasedOn,
    required this.totalAmount,
    required this.currency,
    required this.itemCount,
    required this.hasImage,
    required this.source,
    this.shopName,
    this.categoryId,
    this.categoryName,
    this.notes,
  });

  final String id;
  final String? shopName;
  final DateTime purchasedOn;
  final Money totalAmount;
  final String currency;
  final String? categoryId;
  final String? categoryName;
  final String? notes;
  final String source;
  final int itemCount;
  final bool hasImage;

  String get displayName {
    final s = shopName?.trim();
    if (s != null && s.isNotEmpty) return s;
    return categoryName ?? 'Expense';
  }

  factory ExpenseListItem.fromJson(Map<String, dynamic> j) => ExpenseListItem(
    id: (j['id'] ?? '').toString(),
    shopName: j['shopName'] as String?,
    purchasedOn: parsePurchaseDate(j['purchasedOn']) ?? DateTime.now(),
    totalAmount: parseMoney(j['totalAmount']),
    currency: (j['currency'] ?? 'EUR').toString(),
    categoryId: j['categoryId']?.toString(),
    categoryName: j['categoryName'] as String?,
    notes: j['notes'] as String?,
    source: (j['source'] ?? 'manual').toString(),
    itemCount: (j['itemCount'] as num?)?.toInt() ?? 0,
    hasImage: j['hasImage'] == true,
  );
}

/// A full expense, including its line items.
class ExpenseDetail {
  const ExpenseDetail({
    required this.id,
    required this.purchasedOn,
    required this.totalAmount,
    required this.currency,
    required this.items,
    required this.source,
    this.shopName,
    this.categoryId,
    this.categoryName,
    this.paymentMethod,
    this.notes,
    this.imagePath,
    this.createdOn,
  });

  final String id;
  final String? shopName;
  final DateTime purchasedOn;
  final Money totalAmount;
  final String currency;
  final String? categoryId;
  final String? categoryName;
  final String? paymentMethod;
  final String? notes;
  final String? imagePath;
  final String source;
  final DateTime? createdOn;
  final List<ExpenseItem> items;

  bool get hasImage => imagePath != null && imagePath!.isNotEmpty;

  /// What the line items add up to, which can differ from [totalAmount] when
  /// a receipt was entered as a lump sum or the AI missed a line.
  Money get itemsTotal =>
      items.fold<Money>(0, (sum, i) => sum + i.lineTotal);

  bool get itemsMatchTotal =>
      items.isEmpty || (itemsTotal - totalAmount).abs() < 0.01;

  factory ExpenseDetail.fromJson(Map<String, dynamic> j) => ExpenseDetail(
    id: (j['id'] ?? '').toString(),
    shopName: j['shopName'] as String?,
    purchasedOn: parsePurchaseDate(j['purchasedOn']) ?? DateTime.now(),
    totalAmount: parseMoney(j['totalAmount']),
    currency: (j['currency'] ?? 'EUR').toString(),
    categoryId: j['categoryId']?.toString(),
    categoryName: j['categoryName'] as String?,
    paymentMethod: j['paymentMethod'] as String?,
    notes: j['notes'] as String?,
    imagePath: j['imagePath'] as String?,
    source: (j['source'] ?? 'manual').toString(),
    createdOn: parseApiInstant(j['createdOn']),
    items: ((j['items'] as List?) ?? const [])
        .whereType<Map>()
        .map((e) => ExpenseItem.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false),
  );
}

class Budget {
  const Budget({
    required this.id,
    required this.categoryId,
    required this.categoryName,
    required this.monthlyLimit,
    required this.currency,
  });

  final String id;
  final String categoryId;
  final String categoryName;
  final Money monthlyLimit;
  final String currency;

  factory Budget.fromJson(Map<String, dynamic> j) => Budget(
    id: (j['id'] ?? '').toString(),
    categoryId: (j['categoryId'] ?? '').toString(),
    categoryName: (j['categoryName'] ?? '').toString(),
    monthlyLimit: parseMoney(j['monthlyLimit']),
    currency: (j['currency'] ?? 'EUR').toString(),
  );
}

/// Spend in one category for the dashboard month, with its budget if set.
class CategorySpend {
  const CategorySpend({
    required this.categoryName,
    required this.spent,
    required this.overBudget,
    this.categoryId,
    this.budgetLimit,
  });

  final String? categoryId;
  final String categoryName;
  final Money spent;
  final Money? budgetLimit;
  final bool overBudget;

  bool get hasBudget => budgetLimit != null && budgetLimit! > 0;

  /// Progress toward the budget, clamped so a large overspend does not draw a
  /// bar off the end of the screen.
  double get progress =>
      hasBudget ? (spent / budgetLimit!).clamp(0.0, 1.0) : 0;

  /// The true ratio, uncapped — used to decide how alarming to look.
  double get rawProgress => hasBudget ? spent / budgetLimit! : 0;

  Color get color {
    if (!hasBudget) return AppColors.slate;
    if (overBudget) return AppColors.red;
    if (rawProgress >= 0.85) return AppColors.amber;
    return AppColors.green;
  }

  factory CategorySpend.fromJson(Map<String, dynamic> j) => CategorySpend(
    categoryId: j['categoryId']?.toString(),
    categoryName: (j['categoryName'] ?? 'Uncategorised').toString(),
    spent: parseMoney(j['spent']),
    budgetLimit: j['budgetLimit'] == null ? null : parseMoney(j['budgetLimit']),
    overBudget: j['overBudget'] == true,
  );
}

/// The `/finance/dashboard` payload.
class FinanceDashboard {
  const FinanceDashboard({
    required this.monthTotal,
    required this.currency,
    required this.monthExpenseCount,
    required this.year,
    required this.month,
    required this.categories,
    required this.totalBudget,
    required this.anyOverBudget,
  });

  final Money monthTotal;
  final String currency;
  final int monthExpenseCount;
  final int year;
  final int month;
  final List<CategorySpend> categories;
  final Money totalBudget;
  final bool anyOverBudget;

  bool get hasBudget => totalBudget > 0;

  double get budgetProgress =>
      hasBudget ? (monthTotal / totalBudget).clamp(0.0, 1.0) : 0;

  double get rawBudgetProgress => hasBudget ? monthTotal / totalBudget : 0;

  Money get remaining => totalBudget - monthTotal;

  DateTime get monthDate => DateTime(year, month);

  /// Categories worth charting: anything actually spent on, biggest first.
  List<CategorySpend> get spentCategories =>
      categories.where((c) => c.spent > 0).toList()
        ..sort((a, b) => b.spent.compareTo(a.spent));

  factory FinanceDashboard.fromJson(Map<String, dynamic> j) => FinanceDashboard(
    monthTotal: parseMoney(j['monthTotal']),
    currency: (j['currency'] ?? 'EUR').toString(),
    monthExpenseCount: (j['monthExpenseCount'] as num?)?.toInt() ?? 0,
    year: (j['year'] as num?)?.toInt() ?? DateTime.now().year,
    month: (j['month'] as num?)?.toInt() ?? DateTime.now().month,
    totalBudget: parseMoney(j['totalBudget']),
    anyOverBudget: j['anyOverBudget'] == true,
    categories: ((j['categories'] as List?) ?? const [])
        .whereType<Map>()
        .map((e) => CategorySpend.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false),
  );
}
