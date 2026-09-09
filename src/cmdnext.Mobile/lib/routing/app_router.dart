import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/auth/auth_controller.dart';
import '../core/auth/login_screen.dart';
import '../core/theme/app_theme.dart';
import '../features/chat/ui/chat_screen.dart';
import '../features/finance/ui/budgets_screen.dart';
import '../features/finance/ui/expense_detail_screen.dart';
import '../features/finance/ui/expense_list_screen.dart';
import '../features/finance/ui/finance_screen.dart';
import '../features/home/ui/home_screen.dart';
import '../features/spaces/ui/spaces_screen.dart';
import '../features/tasks/ui/board_screen.dart';
import '../features/tasks/ui/calendar_screen.dart';
import '../features/tasks/ui/task_detail_screen.dart';
import '../features/tasks/ui/task_list_screen.dart';
import '../features/tasks/ui/tasks_screen.dart';

/// Bridges a Riverpod provider to `refreshListenable`, which go_router uses to
/// re-run redirects. Without this, signing out would leave the person parked on
/// a protected screen.
class _AuthRefresh extends ChangeNotifier {
  _AuthRefresh(this._ref) {
    _ref.listen<AuthState>(authProvider, (previous, next) {
      if (previous?.status != next.status) notifyListeners();
    });
  }

  // ignore: unused_field — held so the subscription outlives the constructor.
  final Ref _ref;
}

final _rootKey = GlobalKey<NavigatorState>(debugLabel: 'root');
final _shellKey = GlobalKey<NavigatorState>(debugLabel: 'shell');

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = _AuthRefresh(ref);
  ref.onDispose(refresh.dispose);

  return GoRouter(
    navigatorKey: _rootKey,
    initialLocation: '/tasks',
    refreshListenable: refresh,
    redirect: (context, state) {
      final auth = ref.read(authProvider);
      final loc = state.matchedLocation;

      // Hold on the splash until the keychain has been read, so the login
      // screen never flashes for an already-signed-in person.
      if (!auth.isResolved) return loc == '/splash' ? null : '/splash';

      final atLogin = loc == '/login';
      if (!auth.isSignedIn) return atLogin ? null : '/login';
      if (atLogin || loc == '/splash') return '/tasks';
      return null;
    },
    routes: [
      GoRoute(
        path: '/splash',
        builder: (_, _) => const _SplashScreen(),
      ),
      GoRoute(
        path: '/login',
        builder: (_, _) => const LoginScreen(),
      ),
      GoRoute(
        path: '/tasks/:id',
        parentNavigatorKey: _rootKey,
        builder: (_, state) =>
            TaskDetailScreen(taskId: state.pathParameters['id']!),
      ),
      ShellRoute(
        navigatorKey: _shellKey,
        builder: (context, state, child) =>
            AppShell(location: state.matchedLocation, child: child),
        routes: [
          GoRoute(
            path: '/tasks',
            builder: (_, _) => const TasksScreen(),
            routes: [
              // Nested so the tab bar stays visible and the tab stays selected.
              GoRoute(
                path: 'calendar',
                builder: (_, _) => const CalendarScreen(),
              ),
              GoRoute(
                path: 'all',
                builder: (_, _) => const TaskListScreen(),
              ),
              GoRoute(
                path: 'board',
                builder: (_, _) => const BoardScreen(),
              ),
            ],
          ),
          GoRoute(
            path: '/finance',
            builder: (_, _) => const FinanceScreen(),
            routes: [
              GoRoute(
                path: 'expenses',
                builder: (_, _) => const ExpenseListScreen(),
                routes: [
                  GoRoute(
                    path: ':id',
                    builder: (_, state) => ExpenseDetailScreen(
                      expenseId: state.pathParameters['id']!,
                    ),
                  ),
                ],
              ),
              GoRoute(
                path: 'budgets',
                builder: (_, _) => const BudgetsScreen(),
              ),
            ],
          ),
          GoRoute(
            path: '/chat',
            builder: (_, _) => const ChatScreen(),
          ),
          GoRoute(
            path: '/spaces',
            builder: (_, _) => const SpacesScreen(),
          ),
          GoRoute(
            path: '/home',
            builder: (_, _) => const HomeScreen(),
          ),
        ],
      ),
    ],
  );
});

/// Bottom-tab chrome. The web client uses a sidebar; on a phone the same
/// destinations belong in a thumb-reachable bar.
class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.location, required this.child});

  final String location;
  final Widget child;

  static const _tabs = <({String path, IconData icon, IconData active, String label})>[
    (
      path: '/tasks',
      icon: Icons.check_circle_outline_rounded,
      active: Icons.check_circle_rounded,
      label: 'Tasks',
    ),
    (
      path: '/finance',
      icon: Icons.account_balance_wallet_outlined,
      active: Icons.account_balance_wallet_rounded,
      label: 'Money',
    ),
    (
      path: '/chat',
      icon: Icons.auto_awesome_outlined,
      active: Icons.auto_awesome_rounded,
      label: 'Ask',
    ),
    (
      path: '/spaces',
      icon: Icons.layers_outlined,
      active: Icons.layers_rounded,
      label: 'Spaces',
    ),
    (
      path: '/home',
      icon: Icons.person_outline_rounded,
      active: Icons.person_rounded,
      label: 'You',
    ),
  ];

  int get _index {
    final i = _tabs.indexWhere((t) => location.startsWith(t.path));
    return i < 0 ? 0 : i;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) {
          final target = _tabs[i].path;
          if (target != location) context.go(target);
        },
        destinations: [
          for (final (i, t) in _tabs.indexed)
            NavigationDestination(
              icon: Icon(t.icon),
              selectedIcon: Icon(t.active, color: AppColors.accent),
              label: t.label,
              tooltip: '',
              key: ValueKey('tab-$i'),
            ),
        ],
      ),
    );
  }
}

class _SplashScreen extends StatelessWidget {
  const _SplashScreen();

  @override
  Widget build(BuildContext context) => const Scaffold(
    body: Center(
      child: SizedBox(
        width: 26,
        height: 26,
        child: CircularProgressIndicator(strokeWidth: 2.6),
      ),
    ),
  );
}
