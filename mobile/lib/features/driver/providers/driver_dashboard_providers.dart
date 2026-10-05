import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/providers/auth_provider.dart';
import '../data/driver_repository.dart';
import '../models/assignment_detail_model.dart';
import '../models/driver_availability_status.dart';
import '../models/driver_self_model.dart';

/// Provides the driver's own profile and availability for the authenticated user.
final driverSelfProvider = FutureProvider<DriverSelfModel?>((ref) async {
  final authState = ref.watch(authProvider);
  final userId = authState.user?.id;
  if (userId == null || userId.isEmpty) {
    return null;
  }
  final repository = ref.watch(driverRepositoryProvider);
  return repository.getDriverSelf(userId);
});

/// Provides the driver's currently active assignment detail (InProgress or Assigned), or null.
final driverCurrentAssignmentProvider =
    FutureProvider<AssignmentDetailModel?>((ref) async {
  final repository = ref.watch(driverRepositoryProvider);
  return repository.getCurrentAssignmentDetail();
});

/// Helper class encapsulating authoritative stop progress derived from assignment data.
class DriverStopProgress {
  final int totalStops;
  final int completedStops;
  final int failedStops;
  final int pendingStops;
  final int skippedStops;

  const DriverStopProgress({
    required this.totalStops,
    required this.completedStops,
    required this.failedStops,
    required this.pendingStops,
    required this.skippedStops,
  });

  /// Derives stop progress from [AssignmentDetailModel].
  ///
  /// Uses the detailed [RouteReadModel.stops] when available to accurately calculate
  /// Completed, Failed, Pending, and Skipped stops. Otherwise falls back safely to
  /// summary stop count properties.
  factory DriverStopProgress.fromAssignment(AssignmentDetailModel assignment) {
    if (assignment.route != null && assignment.route!.stops.isNotEmpty) {
      final stops = assignment.route!.stops;
      return DriverStopProgress(
        totalStops: stops.length,
        completedStops: stops.where((s) => s.status.isCompleted).length,
        failedStops: stops.where((s) => s.status.isFailed).length,
        pendingStops: stops.where((s) => s.status.isPending).length,
        skippedStops: stops.where((s) => s.status.isSkipped).length,
      );
    }
    return DriverStopProgress(
      totalStops: assignment.stopCount,
      completedStops: assignment.completedStopCount,
      failedStops: assignment.failedStopCount,
      pendingStops: assignment.pendingStopCount,
      skippedStops: 0,
    );
  }

  /// Completion progress as a value between 0.0 and 1.0.
  double get progressFraction =>
      totalStops > 0 ? (completedStops + failedStops + skippedStops) / totalStops : 0.0;
}

/// Controller managing availability mutations with loading, error states, and double-submit prevention.
class DriverAvailabilityController extends Notifier<AsyncValue<void>> {
  @override
  AsyncValue<void> build() {
    return const AsyncValue.data(null);
  }

  /// Updates the driver's duty availability status (Available vs OffDuty).
  ///
  /// Prevents concurrent or duplicate submissions while an update is in progress.
  /// On success, invalidates [driverSelfProvider] to guarantee authoritative server state.
  Future<bool> updateAvailability(DriverAvailabilityStatus status) async {
    if (state.isLoading) return false;
    state = const AsyncValue.loading();
    try {
      final repository = ref.read(driverRepositoryProvider);
      await repository.updateAvailability(status);
      ref.invalidate(driverSelfProvider);
      state = const AsyncValue.data(null);
      return true;
    } catch (e, st) {
      state = AsyncValue.error(e, st);
      return false;
    }
  }
}

/// Provider for [DriverAvailabilityController].
final driverAvailabilityControllerProvider =
    NotifierProvider<DriverAvailabilityController, AsyncValue<void>>(() {
  return DriverAvailabilityController();
});
