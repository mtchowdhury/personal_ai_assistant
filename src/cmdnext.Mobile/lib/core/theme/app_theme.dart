import 'package:flutter/material.dart';

/// Design tokens shared with the web client so the two read as one product.
///
/// The accent and the task status colors are lifted from the Angular app
/// (`styles.scss` and `DefaultTaskStatuses`) — changing them here without
/// changing them there will make the clients drift apart.
abstract final class AppColors {
  /// The web client's primary blue.
  static const accent = Color(0xFF2F6FED);
  static const accentPressed = Color(0xFF2459C4);

  /// Status palette, matching `DefaultTaskStatuses` on the server.
  static const slate = Color(0xFF6B7280); // To do
  static const amber = Color(0xFFC98A2B); // On Hold
  static const red = Color(0xFFC0453B); // Blocked
  static const green = Color(0xFF3F9C6A); // Done
  static const violet = Color(0xFF8B5CF6); // tag: Households
  static const teal = Color(0xFF0D9488); // tag: Learning

  /// Parses a `#rrggbb` color from the API, falling back when absent or junk.
  /// Status and tag colors are user-editable server-side, so this must not throw.
  static Color parse(String? hex, {Color fallback = slate}) {
    if (hex == null) return fallback;
    var h = hex.trim().replaceFirst('#', '');
    if (h.length == 3) {
      h = h.split('').map((c) => '$c$c').join();
    }
    if (h.length == 6) h = 'ff$h';
    if (h.length != 8) return fallback;
    final v = int.tryParse(h, radix: 16);
    return v == null ? fallback : Color(v);
  }
}

/// Spacing scale. Using a scale rather than ad-hoc numbers keeps rhythm
/// consistent across screens built at different times.
abstract final class Gap {
  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 12.0;
  static const lg = 16.0;
  static const xl = 24.0;
  static const xxl = 32.0;
}

abstract final class Radii {
  static const card = 14.0;
  static const sheet = 20.0;
  static const pill = 999.0;
}

abstract final class AppTheme {
  static ThemeData light() => _build(Brightness.light);
  static ThemeData dark() => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    // `fromSeed` harmonises the seed into a tonal palette, which drifts the
    // primary away from the web client's blue. Pinning primary keeps the two
    // apps visually identical while the rest of the palette stays derived.
    final scheme =
        ColorScheme.fromSeed(
          seedColor: AppColors.accent,
          brightness: brightness,
        ).copyWith(
          primary: AppColors.accent,
          onPrimary: Colors.white,
        );
    final isLight = brightness == Brightness.light;

    // A hair off pure white/black: pure white glares, and a near-black lets
    // elevated cards read as raised without needing heavy shadows.
    final surface = isLight ? const Color(0xFFF7F8FA) : const Color(0xFF121316);
    final card = isLight ? Colors.white : const Color(0xFF1C1E22);

    return ThemeData(
      brightness: brightness,
      colorScheme: scheme.copyWith(surface: surface),
      scaffoldBackgroundColor: surface,
      splashFactory: InkSparkle.splashFactory,
      visualDensity: VisualDensity.standard,

      appBarTheme: AppBarTheme(
        backgroundColor: surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0.5,
        centerTitle: false,
        titleTextStyle: TextStyle(
          fontSize: 22,
          fontWeight: FontWeight.w700,
          letterSpacing: -0.4,
          color: scheme.onSurface,
        ),
      ),

      cardTheme: CardThemeData(
        color: card,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(Radii.card),
          side: BorderSide(
            color: isLight
                ? const Color(0xFFE6E8EC)
                : const Color(0xFF2A2D33),
          ),
        ),
      ),

      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: card,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: Gap.lg,
          vertical: Gap.lg,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(Radii.card),
          borderSide: BorderSide(
            color: isLight
                ? const Color(0xFFE6E8EC)
                : const Color(0xFF2A2D33),
          ),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(Radii.card),
          borderSide: BorderSide(
            color: isLight
                ? const Color(0xFFE6E8EC)
                : const Color(0xFF2A2D33),
          ),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(Radii.card),
          borderSide: const BorderSide(color: AppColors.accent, width: 1.6),
        ),
      ),

      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.card),
          ),
        ),
      ),

      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: card,
        surfaceTintColor: Colors.transparent,
        indicatorColor: AppColors.accent.withValues(alpha: 0.14),
        elevation: 0,
        height: 64,
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        labelTextStyle: const WidgetStatePropertyAll(
          TextStyle(fontSize: 11.5, fontWeight: FontWeight.w600),
        ),
      ),

      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: card,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(
            top: Radius.circular(Radii.sheet),
          ),
        ),
      ),

      dividerTheme: DividerThemeData(
        space: 1,
        thickness: 1,
        color: isLight ? const Color(0xFFE6E8EC) : const Color(0xFF2A2D33),
      ),

      snackBarTheme: const SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
      ),
    );
  }
}
