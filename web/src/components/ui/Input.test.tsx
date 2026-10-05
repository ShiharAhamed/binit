import React from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Input } from './Input';

describe('Input component', () => {
  it('renders label and associates with input', () => {
    render(<Input label="Email address" id="email-field" />);
    const input = screen.getByLabelText(/email address/i);
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute('id', 'email-field');
  });

  it('displays error message and sets aria-invalid', () => {
    render(
      <Input
        label="Password"
        id="password-field"
        error="Password must be at least 8 characters"
      />
    );
    const input = screen.getByLabelText(/password/i);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(
      screen.getByText('Password must be at least 8 characters')
    ).toBeInTheDocument();
  });

  it('forwards ref correctly to HTML input element', () => {
    const ref = React.createRef<HTMLInputElement>();
    render(<Input ref={ref} label="Full name" id="name-field" />);
    expect(ref.current).toBeInstanceOf(HTMLInputElement);
  });
});
