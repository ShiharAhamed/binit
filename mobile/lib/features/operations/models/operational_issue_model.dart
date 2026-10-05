/// Type classification of operational issues reported by drivers in the field.
/// Matches ASP.NET Core `OperationalIssueType` enum serialization.
enum OperationalIssueType {
  vehicleProblem('VehicleProblem', 'Vehicle Problem'),
  roadOrAccessIssue('RoadOrAccessIssue', 'Road / Access Issue'),
  equipmentProblem('EquipmentProblem', 'Equipment Problem'),
  safetyConcern('SafetyConcern', 'Safety Concern'),
  operationalDelay('OperationalDelay', 'Operational Delay'),
  other('Other', 'Other');

  final String value;
  final String displayName;

  const OperationalIssueType(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string or integer representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known OperationalIssueType.
  static OperationalIssueType fromJsonValue(dynamic value) {
    if (value is int) {
      if (value >= 0 && value < OperationalIssueType.values.length) {
        return OperationalIssueType.values[value];
      }
      throw FormatException("Unknown OperationalIssueType integer value: '$value'");
    }

    if (value is String) {
      final trimmed = value.trim();
      for (final type in OperationalIssueType.values) {
        if (type.value.toLowerCase() == trimmed.toLowerCase() ||
            type.name.toLowerCase() == trimmed.toLowerCase()) {
          return type;
        }
      }
      throw FormatException("Unknown OperationalIssueType value: '$value'");
    }

    throw FormatException("Invalid OperationalIssueType type: '${value.runtimeType}'");
  }

  @override
  String toString() => value;
}

/// Lifecycle status of an operational issue reported by a driver.
/// Matches ASP.NET Core `OperationalIssueStatus` enum serialization.
enum OperationalIssueStatus {
  reported('Reported', 'Reported'),
  inReview('InReview', 'In Review'),
  resolved('Resolved', 'Resolved');

  final String value;
  final String displayName;

  const OperationalIssueStatus(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string or integer representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known OperationalIssueStatus.
  static OperationalIssueStatus fromJsonValue(dynamic value) {
    if (value is int) {
      if (value >= 0 && value < OperationalIssueStatus.values.length) {
        return OperationalIssueStatus.values[value];
      }
      throw FormatException("Unknown OperationalIssueStatus integer value: '$value'");
    }

    if (value is String) {
      final trimmed = value.trim();
      for (final status in OperationalIssueStatus.values) {
        if (status.value.toLowerCase() == trimmed.toLowerCase() ||
            status.name.toLowerCase() == trimmed.toLowerCase()) {
          return status;
        }
      }
      throw FormatException("Unknown OperationalIssueStatus value: '$value'");
    }

    throw FormatException("Invalid OperationalIssueStatus type: '${value.runtimeType}'");
  }

  bool get isReported => this == OperationalIssueStatus.reported;
  bool get isInReview => this == OperationalIssueStatus.inReview;
  bool get isResolved => this == OperationalIssueStatus.resolved;

  @override
  String toString() => value;
}

/// Lightweight summary model representing an operational issue in a list.
/// Matches ASP.NET Core `OperationalIssueSummaryDto`.
class OperationalIssueSummaryModel {
  final String id;
  final String driverId;
  final String? driverName;
  final OperationalIssueType issueType;
  final String title;
  final OperationalIssueStatus status;
  final double? latitude;
  final double? longitude;
  final DateTime createdAt;
  final DateTime? updatedAt;

  const OperationalIssueSummaryModel({
    required this.id,
    required this.driverId,
    this.driverName,
    required this.issueType,
    required this.title,
    required this.status,
    this.latitude,
    this.longitude,
    required this.createdAt,
    this.updatedAt,
  });

  bool get hasLocation => latitude != null && longitude != null;

  factory OperationalIssueSummaryModel.fromJson(Map<String, dynamic> json) {
    final idRaw = json['id'];
    if (idRaw == null || idRaw is! String || idRaw.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'id'");
    }

    final driverIdRaw = json['driverId'];
    if (driverIdRaw == null || driverIdRaw is! String || driverIdRaw.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'driverId'");
    }

    final issueTypeRaw = json['issueType'];
    if (issueTypeRaw == null) {
      throw const FormatException("Missing or invalid required field 'issueType'");
    }
    final issueType = OperationalIssueType.fromJsonValue(issueTypeRaw);

    final titleRaw = json['title'];
    if (titleRaw == null || titleRaw is! String) {
      throw const FormatException("Missing or invalid required field 'title'");
    }

    final statusRaw = json['status'];
    if (statusRaw == null) {
      throw const FormatException("Missing or invalid required field 'status'");
    }
    final status = OperationalIssueStatus.fromJsonValue(statusRaw);

    final createdAtRaw = json['createdAt'];
    if (createdAtRaw == null || createdAtRaw is! String) {
      throw const FormatException("Missing or invalid required field 'createdAt'");
    }
    final createdAt = DateTime.parse(createdAtRaw);

    DateTime? updatedAt;
    final updatedAtRaw = json['updatedAt'];
    if (updatedAtRaw != null && updatedAtRaw is String && updatedAtRaw.isNotEmpty) {
      updatedAt = DateTime.parse(updatedAtRaw);
    }

    double? latitude;
    if (json['latitude'] != null) {
      latitude = (json['latitude'] as num).toDouble();
    }

    double? longitude;
    if (json['longitude'] != null) {
      longitude = (json['longitude'] as num).toDouble();
    }

    return OperationalIssueSummaryModel(
      id: idRaw,
      driverId: driverIdRaw,
      driverName: json['driverName'] as String?,
      issueType: issueType,
      title: titleRaw,
      status: status,
      latitude: latitude,
      longitude: longitude,
      createdAt: createdAt,
      updatedAt: updatedAt,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'driverId': driverId,
      if (driverName != null) 'driverName': driverName,
      'issueType': issueType.toJsonValue(),
      'title': title,
      'status': status.toJsonValue(),
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
      'createdAt': createdAt.toIso8601String(),
      if (updatedAt != null) 'updatedAt': updatedAt!.toIso8601String(),
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is OperationalIssueSummaryModel &&
          runtimeType == other.runtimeType &&
          id == other.id;

  @override
  int get hashCode => id.hashCode;
}

/// Comprehensive detail model for an operational issue.
/// Matches ASP.NET Core `OperationalIssueDetailDto`.
class OperationalIssueDetailModel {
  final String id;
  final String driverId;
  final String? driverName;
  final OperationalIssueType issueType;
  final String title;
  final String description;
  final double? latitude;
  final double? longitude;
  final String? locationDescription;
  final OperationalIssueStatus status;
  final String? resolutionNote;
  final DateTime? resolvedAt;
  final String? resolvedByUserId;
  final String? resolvedByUserName;
  final DateTime createdAt;
  final DateTime? updatedAt;

  const OperationalIssueDetailModel({
    required this.id,
    required this.driverId,
    this.driverName,
    required this.issueType,
    required this.title,
    required this.description,
    this.latitude,
    this.longitude,
    this.locationDescription,
    required this.status,
    this.resolutionNote,
    this.resolvedAt,
    this.resolvedByUserId,
    this.resolvedByUserName,
    required this.createdAt,
    this.updatedAt,
  });

  bool get hasLocation => latitude != null && longitude != null;

  factory OperationalIssueDetailModel.fromJson(Map<String, dynamic> json) {
    final idRaw = json['id'];
    if (idRaw == null || idRaw is! String || idRaw.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'id'");
    }

    final driverIdRaw = json['driverId'];
    if (driverIdRaw == null || driverIdRaw is! String || driverIdRaw.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'driverId'");
    }

    final issueTypeRaw = json['issueType'];
    if (issueTypeRaw == null) {
      throw const FormatException("Missing or invalid required field 'issueType'");
    }
    final issueType = OperationalIssueType.fromJsonValue(issueTypeRaw);

    final titleRaw = json['title'];
    if (titleRaw == null || titleRaw is! String) {
      throw const FormatException("Missing or invalid required field 'title'");
    }

    final descriptionRaw = json['description'];
    if (descriptionRaw == null || descriptionRaw is! String) {
      throw const FormatException("Missing or invalid required field 'description'");
    }

    final statusRaw = json['status'];
    if (statusRaw == null) {
      throw const FormatException("Missing or invalid required field 'status'");
    }
    final status = OperationalIssueStatus.fromJsonValue(statusRaw);

    final createdAtRaw = json['createdAt'];
    if (createdAtRaw == null || createdAtRaw is! String) {
      throw const FormatException("Missing or invalid required field 'createdAt'");
    }
    final createdAt = DateTime.parse(createdAtRaw);

    DateTime? updatedAt;
    final updatedAtRaw = json['updatedAt'];
    if (updatedAtRaw != null && updatedAtRaw is String && updatedAtRaw.isNotEmpty) {
      updatedAt = DateTime.parse(updatedAtRaw);
    }

    DateTime? resolvedAt;
    final resolvedAtRaw = json['resolvedAt'];
    if (resolvedAtRaw != null && resolvedAtRaw is String && resolvedAtRaw.isNotEmpty) {
      resolvedAt = DateTime.parse(resolvedAtRaw);
    }

    double? latitude;
    if (json['latitude'] != null) {
      latitude = (json['latitude'] as num).toDouble();
    }

    double? longitude;
    if (json['longitude'] != null) {
      longitude = (json['longitude'] as num).toDouble();
    }

    return OperationalIssueDetailModel(
      id: idRaw,
      driverId: driverIdRaw,
      driverName: json['driverName'] as String?,
      issueType: issueType,
      title: titleRaw,
      description: descriptionRaw,
      latitude: latitude,
      longitude: longitude,
      locationDescription: json['locationDescription'] as String?,
      status: status,
      resolutionNote: json['resolutionNote'] as String?,
      resolvedAt: resolvedAt,
      resolvedByUserId: json['resolvedByUserId'] as String?,
      resolvedByUserName: json['resolvedByUserName'] as String?,
      createdAt: createdAt,
      updatedAt: updatedAt,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'driverId': driverId,
      if (driverName != null) 'driverName': driverName,
      'issueType': issueType.toJsonValue(),
      'title': title,
      'description': description,
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
      if (locationDescription != null) 'locationDescription': locationDescription,
      'status': status.toJsonValue(),
      if (resolutionNote != null) 'resolutionNote': resolutionNote,
      if (resolvedAt != null) 'resolvedAt': resolvedAt!.toIso8601String(),
      if (resolvedByUserId != null) 'resolvedByUserId': resolvedByUserId,
      if (resolvedByUserName != null) 'resolvedByUserName': resolvedByUserName,
      'createdAt': createdAt.toIso8601String(),
      if (updatedAt != null) 'updatedAt': updatedAt!.toIso8601String(),
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is OperationalIssueDetailModel &&
          runtimeType == other.runtimeType &&
          id == other.id;

  @override
  int get hashCode => id.hashCode;
}

/// Paginated result envelope for operational issues.
/// Matches ASP.NET Core `PagedResult<OperationalIssueSummaryDto>`.
class PagedOperationalIssuesModel {
  final List<OperationalIssueSummaryModel> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  const PagedOperationalIssuesModel({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  bool get hasMore => page < totalPages;
  bool get isEmpty => items.isEmpty;
  bool get isNotEmpty => items.isNotEmpty;

  factory PagedOperationalIssuesModel.fromJson(Map<String, dynamic> json) {
    final itemsRaw = json['items'];
    if (itemsRaw == null || itemsRaw is! List) {
      throw const FormatException("Missing or invalid required field 'items'");
    }

    final items = itemsRaw.map((item) {
      if (item is! Map<String, dynamic>) {
        throw const FormatException("Invalid item object in 'items' list");
      }
      return OperationalIssueSummaryModel.fromJson(item);
    }).toList();

    final pageRaw = json['page'];
    if (pageRaw == null || pageRaw is! num) {
      throw const FormatException("Missing or invalid required field 'page'");
    }
    final page = pageRaw.toInt();

    final pageSizeRaw = json['pageSize'];
    if (pageSizeRaw == null || pageSizeRaw is! num) {
      throw const FormatException("Missing or invalid required field 'pageSize'");
    }
    final pageSize = pageSizeRaw.toInt();

    final totalCountRaw = json['totalCount'];
    if (totalCountRaw == null || totalCountRaw is! num) {
      throw const FormatException("Missing or invalid required field 'totalCount'");
    }
    final totalCount = totalCountRaw.toInt();

    final totalPagesRaw = json['totalPages'];
    if (totalPagesRaw == null || totalPagesRaw is! num) {
      throw const FormatException("Missing or invalid required field 'totalPages'");
    }
    final totalPages = totalPagesRaw.toInt();

    return PagedOperationalIssuesModel(
      items: items,
      page: page,
      pageSize: pageSize,
      totalCount: totalCount,
      totalPages: totalPages,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'items': items.map((item) => item.toJson()).toList(),
      'page': page,
      'pageSize': pageSize,
      'totalCount': totalCount,
      'totalPages': totalPages,
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is PagedOperationalIssuesModel &&
          runtimeType == other.runtimeType &&
          page == other.page &&
          pageSize == other.pageSize &&
          totalCount == other.totalCount &&
          totalPages == other.totalPages;

  @override
  int get hashCode =>
      page.hashCode ^
      pageSize.hashCode ^
      totalCount.hashCode ^
      totalPages.hashCode;
}
