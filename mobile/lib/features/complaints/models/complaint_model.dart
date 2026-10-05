/// Category representing citizen service complaints.
/// Matches ASP.NET Core `ComplaintCategory` enum serialization.
enum ComplaintCategory {
  missedCollection('MissedCollection', 'Missed Collection'),
  delayedService('DelayedService', 'Delayed Service'),
  poorService('PoorService', 'Poor Service'),
  unresolvedIssue('UnresolvedIssue', 'Unresolved Issue'),
  other('Other', 'Other');

  final String value;
  final String displayName;

  const ComplaintCategory(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string or integer representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known ComplaintCategory.
  static ComplaintCategory fromJsonValue(dynamic value) {
    if (value is int) {
      if (value >= 0 && value < ComplaintCategory.values.length) {
        return ComplaintCategory.values[value];
      }
      throw FormatException("Unknown ComplaintCategory integer value: '$value'");
    }

    if (value is String) {
      final trimmed = value.trim();
      for (final cat in ComplaintCategory.values) {
        if (cat.value.toLowerCase() == trimmed.toLowerCase() ||
            cat.name.toLowerCase() == trimmed.toLowerCase()) {
          return cat;
        }
      }
      throw FormatException("Unknown ComplaintCategory value: '$value'");
    }

    throw FormatException("Invalid ComplaintCategory type: '${value.runtimeType}'");
  }

  @override
  String toString() => value;
}

/// Lifecycle status of a citizen service complaint.
/// Matches ASP.NET Core `ComplaintStatus` enum serialization.
enum ComplaintStatus {
  submitted('Submitted', 'Submitted'),
  inReview('InReview', 'In Review'),
  resolved('Resolved', 'Resolved');

  final String value;
  final String displayName;

  const ComplaintStatus(this.value, this.displayName);

  /// Serializes to the exact string expected by ASP.NET Core.
  String toJsonValue() => value;

  /// Parses from the string or integer representation returned by ASP.NET Core.
  /// Throws [FormatException] if [value] does not match any known ComplaintStatus.
  static ComplaintStatus fromJsonValue(dynamic value) {
    if (value is int) {
      if (value >= 0 && value < ComplaintStatus.values.length) {
        return ComplaintStatus.values[value];
      }
      throw FormatException("Unknown ComplaintStatus integer value: '$value'");
    }

    if (value is String) {
      final trimmed = value.trim();
      for (final status in ComplaintStatus.values) {
        if (status.value.toLowerCase() == trimmed.toLowerCase() ||
            status.name.toLowerCase() == trimmed.toLowerCase()) {
          return status;
        }
      }
      throw FormatException("Unknown ComplaintStatus value: '$value'");
    }

    throw FormatException("Invalid ComplaintStatus type: '${value.runtimeType}'");
  }

  bool get isSubmitted => this == ComplaintStatus.submitted;
  bool get isInReview => this == ComplaintStatus.inReview;
  bool get isResolved => this == ComplaintStatus.resolved;

  @override
  String toString() => value;
}

/// Lightweight summary model representing a citizen complaint in a list.
/// Matches ASP.NET Core `ComplaintSummaryDto`.
class ComplaintSummaryModel {
  final String id;
  final String citizenId;
  final String? citizenName;
  final ComplaintCategory category;
  final String subject;
  final ComplaintStatus status;
  final double? latitude;
  final double? longitude;
  final DateTime createdAt;
  final DateTime? updatedAt;

  const ComplaintSummaryModel({
    required this.id,
    required this.citizenId,
    this.citizenName,
    required this.category,
    required this.subject,
    required this.status,
    this.latitude,
    this.longitude,
    required this.createdAt,
    this.updatedAt,
  });

  bool get hasLocation => latitude != null && longitude != null;

  factory ComplaintSummaryModel.fromJson(Map<String, dynamic> json) {
    final idRaw = json['id'];
    if (idRaw == null || idRaw is! String || idRaw.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'id'");
    }

    final citizenIdRaw = json['citizenId'];
    if (citizenIdRaw == null || citizenIdRaw is! String || citizenIdRaw.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'citizenId'");
    }

    final categoryRaw = json['category'];
    if (categoryRaw == null) {
      throw const FormatException("Missing or invalid required field 'category'");
    }
    final category = ComplaintCategory.fromJsonValue(categoryRaw);

    final subjectRaw = json['subject'];
    if (subjectRaw == null || subjectRaw is! String) {
      throw const FormatException("Missing or invalid required field 'subject'");
    }

    final statusRaw = json['status'];
    if (statusRaw == null) {
      throw const FormatException("Missing or invalid required field 'status'");
    }
    final status = ComplaintStatus.fromJsonValue(statusRaw);

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

    return ComplaintSummaryModel(
      id: idRaw,
      citizenId: citizenIdRaw,
      citizenName: json['citizenName'] as String?,
      category: category,
      subject: subjectRaw,
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
      'citizenId': citizenId,
      if (citizenName != null) 'citizenName': citizenName,
      'category': category.toJsonValue(),
      'subject': subject,
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
      other is ComplaintSummaryModel &&
          runtimeType == other.runtimeType &&
          id == other.id;

  @override
  int get hashCode => id.hashCode;
}

/// Comprehensive detail model for a citizen complaint.
/// Matches ASP.NET Core `ComplaintDetailDto`.
class ComplaintDetailModel {
  final String id;
  final String citizenId;
  final String? citizenName;
  final ComplaintCategory category;
  final String subject;
  final String description;
  final double? latitude;
  final double? longitude;
  final String? locationDescription;
  final ComplaintStatus status;
  final String? resolutionNote;
  final DateTime? resolvedAt;
  final String? resolvedByUserId;
  final String? resolvedByUserName;
  final DateTime createdAt;
  final DateTime? updatedAt;

  const ComplaintDetailModel({
    required this.id,
    required this.citizenId,
    this.citizenName,
    required this.category,
    required this.subject,
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

  factory ComplaintDetailModel.fromJson(Map<String, dynamic> json) {
    final idRaw = json['id'];
    if (idRaw == null || idRaw is! String || idRaw.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'id'");
    }

    final citizenIdRaw = json['citizenId'];
    if (citizenIdRaw == null || citizenIdRaw is! String || citizenIdRaw.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'citizenId'");
    }

    final categoryRaw = json['category'];
    if (categoryRaw == null) {
      throw const FormatException("Missing or invalid required field 'category'");
    }
    final category = ComplaintCategory.fromJsonValue(categoryRaw);

    final subjectRaw = json['subject'];
    if (subjectRaw == null || subjectRaw is! String) {
      throw const FormatException("Missing or invalid required field 'subject'");
    }

    final descriptionRaw = json['description'];
    if (descriptionRaw == null || descriptionRaw is! String) {
      throw const FormatException("Missing or invalid required field 'description'");
    }

    final statusRaw = json['status'];
    if (statusRaw == null) {
      throw const FormatException("Missing or invalid required field 'status'");
    }
    final status = ComplaintStatus.fromJsonValue(statusRaw);

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

    return ComplaintDetailModel(
      id: idRaw,
      citizenId: citizenIdRaw,
      citizenName: json['citizenName'] as String?,
      category: category,
      subject: subjectRaw,
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
      'citizenId': citizenId,
      if (citizenName != null) 'citizenName': citizenName,
      'category': category.toJsonValue(),
      'subject': subject,
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
      other is ComplaintDetailModel &&
          runtimeType == other.runtimeType &&
          id == other.id;

  @override
  int get hashCode => id.hashCode;
}

/// Paginated result envelope for complaints.
/// Matches ASP.NET Core `PagedResult<ComplaintSummaryDto>`.
class PagedComplaintsModel {
  final List<ComplaintSummaryModel> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  const PagedComplaintsModel({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  bool get hasMore => page < totalPages;
  bool get isEmpty => items.isEmpty;
  bool get isNotEmpty => items.isNotEmpty;

  factory PagedComplaintsModel.fromJson(Map<String, dynamic> json) {
    final itemsRaw = json['items'];
    if (itemsRaw == null || itemsRaw is! List) {
      throw const FormatException("Missing or invalid required field 'items'");
    }

    final items = itemsRaw.map((item) {
      if (item is! Map<String, dynamic>) {
        throw const FormatException("Invalid item object in 'items' list");
      }
      return ComplaintSummaryModel.fromJson(item);
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

    return PagedComplaintsModel(
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
      other is PagedComplaintsModel &&
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
