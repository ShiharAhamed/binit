import 'package:flutter/material.dart';

/// Centralized color palette for SmartWaste mobile application,
/// aligned with the SmartWaste product identity and React web theme.
abstract final class AppColors {
  // Brand / Emerald Scale
  static const Color primary = Color(0xFF059669); // Emerald 600
  static const Color primaryDark = Color(0xFF064E3B); // Forest green / Emerald 900
  static const Color primaryHover = Color(0xFF047857); // Emerald 700
  static const Color primaryAccent = Color(0xFF10B981); // Emerald 500
  static const Color primaryLight = Color(0xFFECFDF5); // Mint / Emerald 50
  static const Color primaryBorder = Color(0xFFA7F3D0); // Emerald 200

  // Neutral / Surface & Background Scale
  static const Color background = Color(0xFFF8FAFC); // Slate 50 cool gray
  static const Color dashboardBackground = Color(0xFFF2FDF9); // Soft light mint green
  static const Color surface = Color(0xFFFFFFFF); // White surface
  static const Color surfaceSubtle = Color(0xFFF1F5F9); // Slate 100
  static const Color border = Color(0xFFE2E8F0); // Slate 200
  static const Color borderSubtle = Color(0xFFF1F5F9); // Slate 100
  static const Color divider = Color(0xFFE2E8F0);

  // Typography / Slate Scale
  static const Color textPrimary = Color(0xFF0F172A); // Slate 900
  static const Color textSecondary = Color(0xFF64748B); // Slate 500
  static const Color textMuted = Color(0xFF94A3B8); // Slate 400
  static const Color textInverse = Color(0xFFFFFFFF);

  // Semantic Feedback
  static const Color success = Color(0xFF16A34A);
  static const Color successLight = Color(0xFFDCFCE7);
  static const Color successBorder = Color(0xFF86EFAC);
  static const Color successText = Color(0xFF15803D);

  static const Color warning = Color(0xFFD97706); // Amber 600
  static const Color warningLight = Color(0xFFFEF3C7); // Amber 100
  static const Color warningBorder = Color(0xFFFCD34D); // Amber 300
  static const Color warningText = Color(0xFF92400E); // Amber 800

  static const Color error = Color(0xFFDC2626); // Red 600
  static const Color errorLight = Color(0xFFFEF2F2); // Red 50
  static const Color errorBorder = Color(0xFFFECACA); // Red 200
  static const Color errorText = Color(0xFF991B1B); // Red 800

  static const Color info = Color(0xFF0284C7); // Sky 600
  static const Color infoLight = Color(0xFFF0F9FF); // Sky 50
  static const Color infoBorder = Color(0xFFBAE6FD); // Sky 200
  static const Color infoText = Color(0xFF075985); // Sky 800
}
