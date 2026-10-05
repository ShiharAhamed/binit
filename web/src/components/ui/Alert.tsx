import React from 'react';

export interface AlertProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: 'error' | 'success' | 'info' | 'warning';
  title?: string;
  children: React.ReactNode;
}

const variantStyles: Record<
  NonNullable<AlertProps['variant']>,
  { container: string; title: string; defaultRole: string }
> = {
  error: {
    container: 'bg-red-50 text-red-800 border-red-200',
    title: 'text-red-900',
    defaultRole: 'alert',
  },
  success: {
    container: 'bg-emerald-50 text-emerald-800 border-emerald-200',
    title: 'text-emerald-900',
    defaultRole: 'status',
  },
  info: {
    container: 'bg-blue-50 text-blue-800 border-blue-200',
    title: 'text-blue-900',
    defaultRole: 'status',
  },
  warning: {
    container: 'bg-amber-50 text-amber-800 border-amber-200',
    title: 'text-amber-900',
    defaultRole: 'alert',
  },
};

export const Alert: React.FC<AlertProps> = ({
  variant = 'info',
  title,
  children,
  className = '',
  role,
  ...props
}) => {
  const config = variantStyles[variant];
  const appliedRole = role || config.defaultRole;

  return (
    <div
      role={appliedRole}
      className={`rounded-lg border p-4 text-sm leading-relaxed ${config.container} ${className}`}
      {...props}
    >
      {title && <h3 className={`font-semibold mb-1 ${config.title}`}>{title}</h3>}
      <div>{children}</div>
    </div>
  );
};
