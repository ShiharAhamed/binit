/// Display-only reference. The full report ID remains the API and navigation identity.
String reportReference(String id, {String? authoritative}) {
  final provided = authoritative?.trim();
  if (provided != null && provided.isNotEmpty) return provided;
  return (id.length > 8 ? id.substring(0, 8) : id).toUpperCase();
}

String reportLabel(String id, {String? authoritative}) =>
    'Report ${reportReference(id, authoritative: authoritative)}';
