import React, { useState } from 'react';
import type { ReportAttachmentDto } from '../types/reporting';
import { PhotoPreviewModal } from './PhotoPreviewModal';

export interface ReportPhotoGalleryProps {
  attachments: ReportAttachmentDto[];
}

export const ReportPhotoGallery: React.FC<ReportPhotoGalleryProps> = ({
  attachments,
}) => {
  const [previewIndex, setPreviewIndex] = useState<number | null>(null);
  const [failedImages, setFailedImages] = useState<Record<string, boolean>>({});

  const handleImageError = (id: string) => {
    setFailedImages((prev) => ({ ...prev, [id]: true }));
  };

  if (!attachments || attachments.length === 0) {
    return (
      <div
        className="py-8 px-4 rounded-lg border border-dashed border-slate-200 bg-slate-50/60 text-center"
        data-testid="no-photo-evidence"
      >
        <div
          className="w-10 h-10 rounded-full bg-slate-100 border border-slate-200 text-slate-400 mx-auto flex items-center justify-center mb-2"
          aria-hidden="true"
        >
          <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={1.5}
              d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"
            />
          </svg>
        </div>
        <p className="text-sm font-medium text-slate-700">No Photo Evidence</p>
        <p className="text-xs text-slate-500 mt-0.5">
          The citizen did not attach photographic evidence with this report.
        </p>
      </div>
    );
  }

  return (
    <>
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4" data-testid="photo-gallery-grid">
        {attachments.map((photo, index) => {
          const isFailed = failedImages[photo.id];

          return (
            <div
              key={photo.id}
              role="button"
              tabIndex={0}
              onClick={() => setPreviewIndex(index)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  setPreviewIndex(index);
                }
              }}
              className="group relative aspect-4/3 rounded-lg overflow-hidden border border-slate-200 bg-slate-100 cursor-pointer shadow-2xs hover:shadow-md transition-all duration-200 focus:outline-hidden focus:ring-2 focus:ring-emerald-500"
              aria-label={`View photo evidence ${index + 1} of ${attachments.length}`}
              data-testid={`photo-thumbnail-${index}`}
            >
              {isFailed ? (
                <div className="w-full h-full flex flex-col items-center justify-center bg-slate-50 text-slate-400 p-3 text-center">
                  <svg
                    className="w-8 h-8 text-slate-300 mb-1.5"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={1.5}
                      d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"
                    />
                  </svg>
                  <span className="text-xs font-medium text-slate-500">Photo unavailable</span>
                  <span className="text-[10px] text-slate-400 mt-0.5">Unable to load image</span>
                </div>
              ) : (
                <>
                  <img
                    src={photo.fileUrl}
                    alt={`Photo evidence ${index + 1}`}
                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                    onError={() => handleImageError(photo.id)}
                    loading="lazy"
                  />
                  {/* Subtle hover gradient and action pill */}
                  <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity flex items-end p-2.5">
                    <span className="inline-flex items-center gap-1 text-[11px] font-medium text-white bg-black/40 backdrop-blur-xs px-2 py-0.5 rounded-full">
                      <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          strokeWidth={2}
                          d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM10 7v6m3-3H7"
                        />
                      </svg>
                      Click to expand
                    </span>
                  </div>
                </>
              )}

              {/* Photo Number Indicator Badge */}
              <div className="absolute top-2 left-2 z-10">
                <span className="bg-slate-900/70 backdrop-blur-xs text-white text-[10px] font-semibold px-2 py-0.5 rounded-full shadow-xs">
                  {index + 1}/{attachments.length}
                </span>
              </div>
            </div>
          );
        })}
      </div>

      {previewIndex !== null && (
        <PhotoPreviewModal
          isOpen={previewIndex !== null}
          photos={attachments}
          currentIndex={previewIndex}
          onClose={() => setPreviewIndex(null)}
          onNavigate={(newIdx) => setPreviewIndex(newIdx)}
        />
      )}
    </>
  );
};
