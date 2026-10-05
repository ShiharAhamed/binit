import 'package:url_launcher/url_launcher.dart';

/// Opens an external navigation app for a saved public-bin destination.
abstract interface class ExternalDirectionsLauncher {
  Future<bool> openDirections({
    required double latitude,
    required double longitude,
  });
}

/// Uses Android's navigation URI first, then a browser-compatible directions URL.
class UrlLauncherExternalDirectionsLauncher
    implements ExternalDirectionsLauncher {
  const UrlLauncherExternalDirectionsLauncher();

  @override
  Future<bool> openDirections({
    required double latitude,
    required double longitude,
  }) async {
    final navigationUri = navigationUriFor(
      latitude: latitude,
      longitude: longitude,
    );
    try {
      if (await launchUrl(
        navigationUri,
        mode: LaunchMode.externalApplication,
      )) {
        return true;
      }
    } catch (_) {
      // Fall through to the browser-compatible directions URL.
    }

    try {
      return await launchUrl(
        browserDirectionsUriFor(latitude: latitude, longitude: longitude),
        mode: LaunchMode.externalApplication,
      );
    } catch (_) {
      return false;
    }
  }

  static Uri navigationUriFor({
    required double latitude,
    required double longitude,
  }) => Uri.parse('google.navigation:q=$latitude,$longitude');

  static Uri browserDirectionsUriFor({
    required double latitude,
    required double longitude,
  }) => Uri.https('www.google.com', '/maps/dir/', {
    'api': '1',
    'destination': '$latitude,$longitude',
  });
}
