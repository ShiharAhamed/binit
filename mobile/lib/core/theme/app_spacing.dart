import 'package:flutter/material.dart';

/// Standard spacing and corner radius scales for SmartWaste mobile application.
abstract final class AppSpacing {
  // Spacing Scale
  static const double xxs = 4.0;
  static const double xs = 8.0;
  static const double sm = 12.0;
  static const double md = 16.0;
  static const double lg = 20.0;
  static const double xl = 24.0;
  static const double xxl = 32.0;
  static const double xxxl = 40.0;

  // Radius Scale
  static const double radiusXs = 4.0;
  static const double radiusSm = 8.0;
  static const double radiusMd = 12.0;
  static const double radiusLg = 16.0;
  static const double radiusXl = 24.0;
  static const double radiusFull = 999.0;

  // Common BorderRadius presets
  static const BorderRadius roundedSm = BorderRadius.all(Radius.circular(radiusSm));
  static const BorderRadius roundedMd = BorderRadius.all(Radius.circular(radiusMd));
  static const BorderRadius roundedLg = BorderRadius.all(Radius.circular(radiusLg));
  static const BorderRadius roundedFull = BorderRadius.all(Radius.circular(radiusFull));

  // Common Padding presets
  static const EdgeInsets screenPadding = EdgeInsets.symmetric(horizontal: xl, vertical: md);
  static const EdgeInsets cardPadding = EdgeInsets.all(xl);
  static const EdgeInsets formGap = EdgeInsets.only(bottom: md);
}
