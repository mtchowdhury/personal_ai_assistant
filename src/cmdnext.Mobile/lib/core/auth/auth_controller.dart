import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/api_exception.dart';
import '../storage/app_storage.dart';
import 'auth_models.dart';

/// Where the app is in its sign-in lifecycle. The router keys off this, so
/// `unknown` exists to keep the login screen from flashing during startup
/// while the keychain is read.
enum AuthStatus { unknown, signedOut, signedIn }

class AuthState {
  const AuthState({required this.status, this.user, this.signingIn = false});

  const AuthState.unknown() : status = AuthStatus.unknown, user = null, signingIn = false;

  final AuthStatus status;
  final AppUser? user;

  /// Kept out of the status so the login form can show progress without the
  /// router treating it as a state change.
  final bool signingIn;

  bool get isSignedIn => status == AuthStatus.signedIn;
  bool get isResolved => status != AuthStatus.unknown;

  AuthState copyWith({AuthStatus? status, AppUser? user, bool? signingIn}) =>
      AuthState(
        status: status ?? this.status,
        user: status == AuthStatus.signedOut ? null : (user ?? this.user),
        signingIn: signingIn ?? this.signingIn,
      );
}

class AuthController extends Notifier<AuthState> {
  @override
  AuthState build() {
    // A 401 from any request ends the session; there is no refresh token.
    final signal = ref.watch(unauthorizedSignalProvider);
    void onUnauthorized() {
      if (state.status == AuthStatus.signedIn) _clear();
    }

    signal.addListener(onUnauthorized);
    ref.onDispose(() => signal.removeListener(onUnauthorized));

    // Deliberately not awaited: `build` is synchronous, and the router shows a
    // splash while the status is `unknown`.
    Future.microtask(restore);
    return const AuthState.unknown();
  }

  SecureTokenStore get _tokens => ref.read(secureTokenStoreProvider);

  /// Restores a session from the keychain on launch.
  ///
  /// The cached user is trusted optimistically so the app opens straight to
  /// content; `/auth/me` then confirms in the background. Without the optimistic
  /// step, launching outside the private network would bounce to the login screen even
  /// though the session is perfectly valid.
  Future<void> restore() async {
    final token = await _tokens.readToken();
    if (token == null || token.isEmpty) {
      state = const AuthState(status: AuthStatus.signedOut);
      return;
    }

    final cached = await _tokens.readUser();
    if (cached != null && cached.isNotEmpty) {
      try {
        state = AuthState(
          status: AuthStatus.signedIn,
          user: AppUser.fromJson(
            jsonDecode(cached) as Map<String, dynamic>,
          ),
        );
      } catch (_) {
        // Corrupt cache is not a reason to sign out; /auth/me will settle it.
      }
    }

    try {
      final json = await ref.read(apiClientProvider).getObject('auth/me');
      final user = AppUser.fromJson(
        (json['user'] as Map?)?.cast<String, dynamic>() ?? const {},
      );
      // `/auth/me` returns `token: null`, so the stored token is left alone.
      await _tokens.writeUser(jsonEncode(user.toJson()));
      state = AuthState(status: AuthStatus.signedIn, user: user);
    } on ApiException catch (e) {
      if (e.isUnauthorized) {
        await _clear();
      } else if (state.status != AuthStatus.signedIn) {
        // Server unreachable and nothing cached — nothing to show but login.
        state = const AuthState(status: AuthStatus.signedOut);
      }
      // Otherwise: offline with a cached session, so stay signed in.
    }
  }

  /// Signs in. Throws [ApiException] so the form can show the real message.
  Future<void> signIn({required String email, required String password}) async {
    state = state.copyWith(signingIn: true);
    try {
      final json = await ref.read(apiClientProvider).post<Map<String, dynamic>>(
        'auth/login',
        // `client` asks the API for the mobile token lifetime. There is no
        // refresh flow, so a short token would mean signing in again every few
        // hours on a personal phone; the token lives in the device keychain.
        body: {
          'email': email.trim(),
          'password': password,
          'client': 'mobile',
        },
      );
      await _persist(AuthResult.fromJson(json));
    } finally {
      // On success the status change already rebuilt the UI; on failure this
      // releases the button so the person can correct and retry.
      if (state.status != AuthStatus.signedIn) {
        state = state.copyWith(signingIn: false);
      }
    }
  }

  Future<void> register({
    required String email,
    required String password,
    String? firstName,
    String? lastName,
  }) async {
    state = state.copyWith(signingIn: true);
    try {
      final json = await ref.read(apiClientProvider).post<Map<String, dynamic>>(
        'auth/register',
        body: {
          'email': email.trim(),
          'password': password,
          'firstName': firstName?.trim(),
          'lastName': lastName?.trim(),
          'client': 'mobile',
        },
      );
      await _persist(AuthResult.fromJson(json));
    } finally {
      if (state.status != AuthStatus.signedIn) {
        state = state.copyWith(signingIn: false);
      }
    }
  }

  Future<void> signOut() => _clear();

  Future<void> _persist(AuthResult result) async {
    await _tokens.writeToken(result.token);
    await _tokens.writeUser(jsonEncode(result.user.toJson()));
    state = AuthState(status: AuthStatus.signedIn, user: result.user);
  }

  Future<void> _clear() async {
    await _tokens.clear();
    state = const AuthState(status: AuthStatus.signedOut);
  }
}

final authProvider = NotifierProvider<AuthController, AuthState>(
  AuthController.new,
);
