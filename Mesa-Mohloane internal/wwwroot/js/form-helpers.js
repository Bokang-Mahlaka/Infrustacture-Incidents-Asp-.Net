// Form helper utilities for async operations and validation

// Show loading spinner on button
function showLoadingState(buttonSelector) {
    const button = document.querySelector(buttonSelector);
    if (!button) return;

    const originalText = button.innerHTML;
    button.innerHTML = '<span class="spinner-border spinner-border-sm mr-2"></span>Loading...';
    button.disabled = true;

    return () => {
        button.innerHTML = originalText;
        button.disabled = false;
    };
}

// Submit form asynchronously with loading state and toast notifications
async function submitFormAsync(event, url, method = 'POST') {
    event.preventDefault();
    const form = event.target;
    const button = form.querySelector('[type="submit"]');

    // Show loading state
    const resetButton = showLoadingState(button);

    try {
        const formData = new FormData(form);
        const response = await fetch(url, {
            method: method,
            body: formData,
            headers: {
                'Accept': 'application/json'
            }
        });

        const result = await response.json();

        if (response.ok) {
            toastr.success(result.message || 'Operation completed successfully');
            
            // Reload or redirect after success
            setTimeout(() => {
                window.location.reload();
            }, 1500);
        } else {
            const errorMsg = result.message || result.errors?.join(', ') || 'An error occurred';
            toastr.error(errorMsg);
        }
    } catch (error) {
        console.error('Form submission error:', error);
        toastr.error('Network error. Please try again.');
    } finally {
        resetButton();
    }
}

// Validate invoice line items total
function validateInvoiceTotal() {
    const lineItemsContainer = document.getElementById('lineItems');
    if (!lineItemsContainer) return;

    const quantityInputs = lineItemsContainer.querySelectorAll('[data-quantity]');
    const priceInputs = lineItemsContainer.querySelectorAll('[data-price]');
    const totalField = document.getElementById('TotalAmount');

    let total = 0;
    const items = [];

    quantityInputs.forEach((qInput, index) => {
        const pInput = priceInputs[index];
        if (qInput && pInput) {
            const qty = parseFloat(qInput.value) || 0;
            const price = parseFloat(pInput.value) || 0;
            const lineTotal = qty * price;
            total += lineTotal;
            items.push(lineTotal);

            // Update line total display
            const lineTotalDisplay = qInput.closest('tr')?.querySelector('[data-line-total]');
            if (lineTotalDisplay) {
                lineTotalDisplay.textContent = lineTotal.toFixed(2);
            }
        }
    });

    // Update total field
    if (totalField) {
        const formattedTotal = total.toFixed(2);
        totalField.value = formattedTotal;

        // Add visual feedback if total mismatch
        const totalDisplay = document.getElementById('totalDisplay');
        if (totalDisplay) {
            totalDisplay.textContent = formattedTotal;
            totalDisplay.classList.toggle('text-danger', false);
        }
    }

    return total;
}

// Real-time line item validation
function attachLineItemValidation() {
    const lineItemsContainer = document.getElementById('lineItems');
    if (!lineItemsContainer) return;

    lineItemsContainer.addEventListener('input', (e) => {
        if (e.target.matches('[data-quantity], [data-price]')) {
            validateInvoiceTotal();
        }
    });
}

// Confirm deletion with modal
async function confirmDelete(url, message = 'Are you sure you want to delete this?') {
    if (confirm(message)) {
        try {
            const response = await fetch(url, { method: 'DELETE' });
            if (response.ok) {
                toastr.success('Deleted successfully');
                setTimeout(() => window.location.reload(), 1500);
            } else {
                toastr.error('Failed to delete');
            }
        } catch (error) {
            toastr.error('Network error');
        }
    }
}

// Initialize on document ready
document.addEventListener('DOMContentLoaded', function() {
    attachLineItemValidation();
});
