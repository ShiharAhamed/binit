import React, { useEffect, useCallback } from 'react';
import type { ReportAttachmentDto } from '../types/reporting';

export interface PhotoPreviewModalProps {
  isOpen: boolean;
  photos: ReportAttachmentDto[];
  currentIndex: number;
  onClose: () => void;
  onNavigate: (newIndex: number) => void;
}

export const PhotoPreviewModal: React.FC<PhotoPreviewModalProps> = ({
  isOpen,
  photos,
  currentIndex,
  onClose,
  onNavigate,
}) => {
  const total = photos.length;
  const currentPhoto = photos[currentIndex];

  const handlePrev = useCallback(() => {
    if (currentIndex > 0) {
      onNavigate(currentIndex - 1);
    } else {
      onNavigate(total - 1); // Wrap around
    }
  }, [currentIndex, total, onNavigate]);

  const handleNext = useCallback(() => {
    if (currentIndex < total - 1) {
      onNavigate(currentIndex + 1);
    } else {
      onNavigate(0); // Wrap around
    }
  }, [currentIndex, total, onNavigate]);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      } else if (e.key === 'ArrowLeft' && total > 1) {
        handlePrev();
      } else if (e.key === 'ArrowRight' && total > 1) {
        handleNext();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    // Prevent body scroll when modal is active
    document.body.style.overflow = 'hidden';

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = 'unset';
    };
  }, [isOpen, onClose, handlePrev, handleNext, total]);

  if (!isOpen || !currentPhoto) {
    return null;
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-slate-900/80 backdrop-blur-xs animate-in fade-in duration-200"
      role="dialog"
      aria-modal="true"
      aria-label="Photo evidence preview"
      onClick={onClose}
    >
      <div
        className="relative max-w-4xl w-full max-h-[90vh] flex flex-col items-center justify-center"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Top Controls Bar */}
        <div className="w-full flex items-center justify-between text-white/90 mb-3 px-2">
          <span className="text-sm font-medium tracking-wide">
            Photo {currentIndex + 1} of {total}
          </span>
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 rounded-full hover:bg-white/10 text-white transition-colors focus:outline-hidden focus:ring-2 focus:ring-emerald-400"
            aria-label="Close photo preview"
          >
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Main Image View Container */}
        <div className="relative w-full flex items-center justify-center overflow-hidden rounded-lg bg-black/40 shadow-2xl">
          <img
            src={currentPhoto.fileUrl}
            alt={`Waste report photo evidence ${currentIndex + 1}`}
            className="max-h-[75vh] w-auto max-w-full object-contain select-none"
          />

          {/* Navigation Controls (when > 1 photo) */}
          {total > 1 && (
            <>
              <button
                type="button"
                onClick={handlePrev}
                className="absolute left-3 top-1/2 -translate-y-1/2 p-2 rounded-full bg-black/50 hover:bg-black/75 text-white transition-colors focus:outline-hidden focus:ring-2 focus:ring-emerald-400"
                aria-label="Previous photo"
              >
                <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M15 19l-7-7 7-7" />
                </svg>
              </button>
              <button
                type="button"
                onClick={handleNext}
                className="absolute right-3 top-1/2 -translate-y-1/2 p-2 rounded-full bg-black/50 hover:bg-black/75 text-white transition-colors focus:outline-hidden focus:ring-2 focus:ring-emerald-400"
                aria-label="Next photo"
              >
                <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M9 5l7 7-7 7" />
                </svg>
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
