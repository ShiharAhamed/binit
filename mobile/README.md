# mobile

A new Flutter project.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Learn Flutter](https://docs.flutter.dev/get-started/learn-flutter)
- [Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Flutter learning resources](https://docs.flutter.dev/reference/learning-resources)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.

## Design System & UI Architecture

SmartWaste Flutter uses a centralized design system aligned with the web product identity:
- **Theme Tokens**: Located in `lib/core/theme/` (`app_colors.dart`, `app_typography.dart`, `app_spacing.dart`, `app_theme.dart`).
- **Shared Widgets**: Located in `lib/shared/widgets/` (`AppButton`, `AppTextField`, `AppCard`, `AppAlert`, `AppLoadingIndicator`).
- **UI Consistency Rule**: All new screens must reuse these theme tokens and shared primitives to maintain brand consistency, responsiveness, and accessibility across the application. Avoid repeated hard-coded colors or local widget duplications.

