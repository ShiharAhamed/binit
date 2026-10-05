import React from 'react';

export interface LoadingSpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  className?: string;
  label?: string;
}

const sizeClasses = {
  sm: 'h-4 w-4 border-2',
  md: 'h-8 w-8 border-3',
  lg: 'h-10 w-10 border-4',
};

export const LoadingSpinner: React.FC<LoadingSpinnerProps> = ({
  size = 'md',
  className = '',
  label = 'Loading...',
}) => {
  return (
    <div
      role="status"
      aria-label={label}
      className={`inline-block animate-spin rounded-full border-slate-200 border-t-emerald-600 ${sizeClasses[size]} ${className}`}
    >
      <span className="sr-only">{label}</span>
    </div>
  );
};
