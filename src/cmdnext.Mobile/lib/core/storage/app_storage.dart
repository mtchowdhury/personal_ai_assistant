import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Where the API lives. Defaults to the the private network hostname, which survives
/// a host address change; overridable at runtime because there is no public
/// hostname and the private network device could be renamed.
const kDefaultServerUrl = 'http://tawhid:8080';

const _kServerUrlKey = 'server_url';
const _kTokenKey = 'auth_token';
const _kUserKey = 'auth_user';

/// Token storage, kept in the keychain rather than prefs: it is a bearer
/// credential with no refresh counterpart, so leaking it leaks the account.
class SecureTokenStore {
  SecureTokenStore(this._storage);

  final FlutterSecureStorage _storage;

  /// `first_unlock` rather than the default `unlocked`: a background refresh
  /// after a reboot should still be able to read the token.
  static const _ios = IOSOptions(
    accessibility: KeychainAccessibility.first_unlock,
  );

  Future<String?> readToken() => _storage.read(key: _kTokenKey, iOptions: _ios);

  Future<void> writeToken(String token) =>
      _storage.write(key: _kTokenKey, value: token, iOptions: _ios);

  Future<String?> readUser() => _storage.read(key: _kUserKey, iOptions: _ios);

  Future<void> writeUser(String json) =>
      _storage.write(key: _kUserKey, value: json, iOptions: _ios);

  Future<void> clear() async {
    await _storage.delete(key: _kTokenKey, iOptions: _ios);
    await _storage.delete(key: _kUserKey, iOptions: _ios);
  }
}

/// The server base URL. Not a secret, so plain prefs.
class ServerUrlStore {
  ServerUrlStore(this._prefs);

  final SharedPreferences _prefs;

  String read() => _prefs.getString(_kServerUrlKey) ?? kDefaultServerUrl;

  Future<void> write(String url) =>
      _prefs.setString(_kServerUrlKey, normalize(url));

  /// Accepts what someone would actually type — `tawhid:8080`, a trailing
  /// slash, an accidental `/api/v1` suffix — and returns a bare origin.
  static String normalize(String input) {
    var url = input.trim();
    if (url.isEmpty) return kDefaultServerUrl;
    if (!url.contains('://')) url = 'http://$url';
    url = url.replaceAll(RegExp(r'/+$'), '');
    url = url.replaceFirst(RegExp(r'/api(/v\d+(\.\d+)?)?$'), '');
    return url;
  }
}

final secureTokenStoreProvider = Provider<SecureTokenStore>(
  (ref) => SecureTokenStore(const FlutterSecureStorage()),
);

/// Resolved once during startup so the rest of the app can read prefs
/// synchronously instead of threading futures through every provider.
final sharedPreferencesProvider = Provider<SharedPreferences>(
  (ref) => throw StateError('sharedPreferencesProvider must be overridden'),
);

final serverUrlStoreProvider = Provider<ServerUrlStore>(
  (ref) => ServerUrlStore(ref.watch(sharedPreferencesProvider)),
);

/// The current server origin. Writing to this rebuilds the Dio client, so a
/// changed server takes effect without a restart.
class ServerUrlController extends Notifier<String> {
  @override
  String build() => ref.watch(serverUrlStoreProvider).read();

  Future<void> set(String url) async {
    final normalized = ServerUrlStore.normalize(url);
    await ref.read(serverUrlStoreProvider).write(normalized);
    state = normalized;
  }
}

final serverUrlProvider = NotifierProvider<ServerUrlController, String>(
  ServerUrlController.new,
);
