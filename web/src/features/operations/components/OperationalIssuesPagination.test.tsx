import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { OperationalIssuesPagination } from './OperationalIssuesPagination';

describe('OperationalIssuesPagination', () => {
  it('renders pagination summary text correctly', () => {
    render(
      <OperationalIssuesPagination
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

  it('disables previous button on first page', () => {
    render(
      <OperationalIssuesPagination
        currentPage={1}
        totalPages={3}
        totalCount={50}
        pageSize={20}
        onPageChange={vi.fn()}
      />
    );

    const prevBtn = screen.getByRole('button', { name: /previous page/i });
    const nextBtn = screen.getByRole('button', { name: /next page/i });

    expect(prevBtn).toBeDisabled();
    expect(nextBtn).toBeEnabled();
  });

  it('disables next button on last page', () => {
    render(
      <OperationalIssuesPagination
        currentPage={3}
        totalPages={3}
        totalCount={50}
        pageSize={20}
        onPageChange={vi.fn()}
      />
    );

    const prevBtn = screen.getByRole('button', { name: /previous page/i });
    const nextBtn = screen.getByRole('button', { name: /next page/i });

    expect(prevBtn).toBeEnabled();
    expect(nextBtn).toBeDisabled();
  });

  it('calls onPageChange when clicking next and previous buttons', () => {
    const handlePageChange = vi.fn();
    render(
      <OperationalIssuesPagination
        currentPage={2}
        totalPages={4}
        totalCount={70}
        pageSize={20}
        onPageChange={handlePageChange}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: /previous page/i }));
    expect(handlePageChange).toHaveBeenCalledWith(1);

    fireEvent.click(screen.getByRole('button', { name: /next page/i }));
    expect(handlePageChange).toHaveBeenCalledWith(3);
  });

  it('renders nothing when totalCount is 0', () => {
    const { container } = render(
      <OperationalIssuesPagination
        currentPage={1}
        totalPages={0}
        totalCount={0}
        pageSize={20}
        onPageChange={vi.fn()}
      />
    );

    expect(container).toBeEmptyDOMElement();
  });
});
