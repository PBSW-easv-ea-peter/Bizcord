// Feedback when a form is submitted:
// the submit button is disabled and shows a spinner, so the user can see that something is happening
// and does not submit twice (e.g. create the same chat twice).
// The text while working is set with data-loading-text="..." on the button.
document.addEventListener('submit', (event) => {
    const form = event.target;
    const button = form.querySelector('button[type="submit"]');
    if (!button || event.defaultPrevented) {
        return;
    }

    // Already submitting: stop a double submit (e.g. Enter pressed twice)
    if (form.dataset.submitting === 'true') {
        event.preventDefault();
        return;
    }
    form.dataset.submitting = 'true';

    const loadingText = button.dataset.loadingText || 'Arbejder …';
    button.dataset.originalHtml = button.innerHTML;
    button.disabled = true;
    button.setAttribute('aria-busy', 'true');
    button.innerHTML =
        '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>' +
        '<span role="status">' + loadingText + '</span>';
});

// If the page is shown again from the browser cache (back button), the buttons must work again.
window.addEventListener('pageshow', (event) => {
    if (!event.persisted) {
        return;
    }
    document.querySelectorAll('form[data-submitting="true"]').forEach((form) => {
        delete form.dataset.submitting;
        const button = form.querySelector('button[type="submit"]');
        if (button && button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
            button.disabled = false;
            button.removeAttribute('aria-busy');
        }
    });
});
