import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ComplaintsPagination } from './ComplaintsPagination';

describe('ComplaintsPagination', () => {
  it('does not render when totalCount is 0', () => {
    const { container } = render(
      <ComplaintsPagination
        currentPage={1}
        totalPages={0}
        totalCount={0}
        pageSize={20}
        onPageChange={vi.fn()}
      />
    );

    expect(container.firstChild).toBeNull();
  });

  it('renders summary and page navigation correctly', () => {
    render(
      <ComplaintsPagination
        currentPage={2}
        totalPages={5}
        totalCount={95}
        pageSize={20}
        onPageChange={vi.fn()}
      />
    );

    expect(screen.getByText(/showing/i)).toBeInTheDocument();
    expect(screen.getByText('21')).toBeInTheDocument();
    expect(screen.getByText('40')).toBeInTheDocument();
    expect(screen.getByText('95')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 5')).toBeInTheDocument();
  });

  it('handles previous and next page changes', () => {
    const onPageChange = vi.fn();
    render(
      <ComplaintsPagination
        currentPage={2}
        totalPages={3}
        totalCount={50}
        pageSize={20}
        onPageChange={onPageChange}
      />
    );

    const prevBtn = screen.getByRole('button', { name: /previous/i });
    const nextBtn = screen.getByRole('button', { name: /next/i });

    expect(prevBtn).not.toBeDisabled();
    expect(nextBtn).not.toBeDisabled();

    fireEvent.click(prevBtn);
    expect(onPageChange).toHaveBeenCalledWith(1);

    fireEvent.click(nextBtn);
    expect(onPageChange).toHaveBeenCalledWith(3);
  });

  it('disables previous button on first page and next button on last page', () => {
    const { rerender } = render(
      <ComplaintsPagination
        currentPage={1}
        totalPages={3}
        totalCount={50}
        pageSize={20}
        onPageChange={vi.fn()}
      />
    );

    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /next/i })).not.toBeDisabled();

    rerender(
      <ComplaintsPagination
        currentPage={3}
        totalPages={3}
        totalCount={50}
        pageSize={20}
        onPageChange={vi.fn()}
      />
    );

    expect(screen.getByRole('button', { name: /previous/i })).not.toBeDisabled();
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled();
  });
});
