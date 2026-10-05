import 'assignment_task_model.dart';
import 'driver_json_helpers.dart';
import 'route_stop_history_model.dart';
import 'route_stop_status.dart';

/// Represents a single stop on a collection route.
class RouteStopModel {
  final String id;
  final int sequence;
  final RouteStopStatus status;
  final DateTime? completedAt;
  final DateTime? failedAt;
  final String? failureReason;
  final AssignmentTaskModel task;
  final List<RouteStopHistoryModel> history;

  const RouteStopModel({
    required this.id,
    required this.sequence,
    required this.status,
    this.completedAt,
    this.failedAt,
    this.failureReason,
    required this.task,
    this.history = const [],
  });

  /// True if the linked task has valid Earth coordinates for map rendering.
  bool get hasValidCoordinates => task.hasValidCoordinates;

  factory RouteStopModel.fromJson(Map<String, dynamic> json) {
    final rawTask = json['task'];
    if (rawTask is! Map<String, dynamic>) {
      throw const FormatException("Missing or invalid required field 'task'");
    }

    final rawHistory = json['history'];
    final historyList = rawHistory is List
        ? rawHistory
            .map((h) => RouteStopHistoryModel.fromJson(h as Map<String, dynamic>))
            .toList(growable: false)
        : const <RouteStopHistoryModel>[];

    return RouteStopModel(
      id: requiredJsonString(json, 'id'),
      sequence: requiredJsonInt(json, 'sequence'),
      status: RouteStopStatus.fromJsonValue(
        requiredJsonString(json, 'status'),
      ),
      completedAt: optionalJsonDateTime(json['completedAt'], 'completedAt'),
      failedAt: optionalJsonDateTime(json['failedAt'], 'failedAt'),
      failureReason: json['failureReason'] as String?,
      task: AssignmentTaskModel.fromJson(rawTask),
      history: historyList,
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'sequence': sequence,
    'status': status.toJsonValue(),
    if (completedAt != null) 'completedAt': completedAt!.toIso8601String(),
    if (failedAt != null) 'failedAt': failedAt!.toIso8601String(),
    if (failureReason != null) 'failureReason': failureReason,
    'task': task.toJson(),
    'history': history.map((h) => h.toJson()).toList(),
  };
}
