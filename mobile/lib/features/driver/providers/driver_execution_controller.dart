import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../data/driver_repository.dart';
import 'driver_dashboard_providers.dart';
import 'driver_history_providers.dart';

/// The single in-flight mutation currently being submitted from Driver execution UI.
enum DriverExecutionOperation { start, completeStop, failStop, finalize }

/// UI state for one authoritative Driver execution request.
class DriverExecutionState {
  final DriverExecutionOperation? operation;
  final String? assignmentId;
  final String? stopId;
  final bool isLoading;
  final bool wasSuccessful;
  final Object? error;

  const DriverExecutionState({
    this.operation,
    this.assignmentId,
    this.stopId,
    this.isLoading = false,
    this.wasSuccessful = false,
    this.error,
  });

  const DriverExecutionState.idle() : this();

  bool isRunningFor(
    DriverExecutionOperation expectedOperation,
    String expectedAssignmentId, {
    String? expectedStopId,
  }) {
    return isLoading &&
        operation == expectedOperation &&
        assignmentId == expectedAssignmentId &&
        stopId == expectedStopId;
  }
}

/// Executes exactly one Driver assignment mutation at a time.
///
/// The backend remains authoritative. Every completed request, including a
/// rejected or transport-ambiguous one, refreshes the current assignment so
/// the screen never assumes that a mutation did or did not commit.
class DriverExecutionController extends Notifier<DriverExecutionState> {
  @override
  DriverExecutionState build() => const DriverExecutionState.idle();

  Future<bool> startAssignment(String assignmentId) {
    return _perform(
      operation: DriverExecutionOperation.start,
      assignmentId: assignmentId,
      request: (repository) => repository.startAssignment(assignmentId),
    );
  }

  Future<bool> completeStop(String assignmentId, String stopId) {
    return _perform(
      operation: DriverExecutionOperation.completeStop,
      assignmentId: assignmentId,
      stopId: stopId,
      request: (repository) => repository.completeStop(assignmentId, stopId),
    );
  }

  Future<bool> failStop(String assignmentId, String stopId, String reason) {
    return _perform(
      operation: DriverExecutionOperation.failStop,
      assignmentId: assignmentId,
      stopId: stopId,
      request: (repository) =>
          repository.failStop(assignmentId, stopId, reason),
    );
  }

  Future<bool> finalizeAssignment(String assignmentId) {
    return _perform(
      operation: DriverExecutionOperation.finalize,
      assignmentId: assignmentId,
      request: (repository) => repository.finalizeAssignment(assignmentId),
    );
  }

  Future<bool> _perform({
    required DriverExecutionOperation operation,
    required String assignmentId,
    String? stopId,
    required Future<void> Function(DriverRepository repository) request,
  }) async {
    if (state.isLoading) return false;

    state = DriverExecutionState(
      operation: operation,
      assignmentId: assignmentId,
      stopId: stopId,
      isLoading: true,
    );

    try {
      await request(ref.read(driverRepositoryProvider));
      await _refreshAuthoritativeAssignment();
      _invalidateTerminalResultsAfterFinalize(operation);
      state = DriverExecutionState(
        operation: operation,
        assignmentId: assignmentId,
        stopId: stopId,
        wasSuccessful: true,
      );
      return true;
    } catch (error) {
      await _refreshAuthoritativeAssignment();
      // A transport-ambiguous finalization can have committed remotely. Refresh
      // terminal read models even when the mutation response itself failed.
      _invalidateTerminalResultsAfterFinalize(operation);
      state = DriverExecutionState(
        operation: operation,
        assignmentId: assignmentId,
        stopId: stopId,
        error: error,
      );
      return false;
    }
  }

  void _invalidateTerminalResultsAfterFinalize(
    DriverExecutionOperation operation,
  ) {
    if (operation != DriverExecutionOperation.finalize) return;
    ref.invalidate(driverRecentAssignmentsProvider);
    ref.invalidate(driverAssignmentHistoryPageProvider);
  }

  Future<void> _refreshAuthoritativeAssignment() async {
    ref.invalidate(driverCurrentAssignmentProvider);
    try {
      await ref.read(driverCurrentAssignmentProvider.future);
    } catch (_) {
      // The page's existing AsyncValue error state presents a failed refresh.
    }
  }
}

final driverExecutionControllerProvider =
    NotifierProvider<DriverExecutionController, DriverExecutionState>(
      DriverExecutionController.new,
    );

/// Returns a concise, actionable mutation error while preserving the backend's
/// readable validation/conflict message whenever it is available.
String driverExecutionErrorMessage(Object? error) {
  if (error is ApiException) {
    if (error.statusCode == 409) {
      return '${error.message} The latest assignment state has been refreshed.';
    }
    if (error.statusCode == null || error.statusCode == 408) {
      return 'The result could not be confirmed. The latest assignment state has been refreshed.';
    }
    return error.message;
  }
  return 'Unable to complete this action. The latest assignment state has been refreshed.';
}
