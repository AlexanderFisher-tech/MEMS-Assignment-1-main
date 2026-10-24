/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Supports print timestamps, dependent position choices, and client upload checks.
 */
'use strict';
document.getElementById('print-guide')?.addEventListener('click', () => window.print());
// Refresh even when the guide has been open for a while, including Ctrl+P / browser-menu printing.
window.addEventListener('beforeprint', () => {
    const date = document.getElementById('guide-print-date');
    if (!date) return;
    const now = new Date();
    date.dateTime = now.toISOString();
    date.textContent = new Intl.DateTimeFormat('en-CA', {
        timeZone: 'America/Vancouver', year: 'numeric', month: 'long', day: 'numeric',
        hour: '2-digit', minute: '2-digit', timeZoneName: 'short'
    }).format(now) + ' (Vancouver)';
});
// Clear a position filter from the old municipality before submitting the new directory selection.
document.getElementById('directory-municipality')?.addEventListener('change', () => { document.getElementById('directory-position').value = ''; document.getElementById('directory-municipality').form.requestSubmit(); });
const municipality = document.getElementById('candidate-municipality');
const positions = document.getElementById('candidate-position');
if (municipality && positions) {
    // Retain all authorized options so switching municipalities can restore previously filtered positions.
    const all = Array.from(positions.options).map(o => o.cloneNode(true));
    const filter = () => {
        const current = positions.value;
        positions.replaceChildren(...all.filter(o => !o.value || o.dataset.municipality === municipality.value).map(o => o.cloneNode(true)));
        if (Array.from(positions.options).some(o => o.value === current)) positions.value = current;
    };
    municipality.addEventListener('change', filter);
    filter();
}
// Client checks provide prompt feedback; ImageStore remains responsible for server-side content validation.
document.querySelectorAll('.image-upload').forEach(input => {
    input.addEventListener('change', () => {
        const file = input.files[0];
        input.setCustomValidity(file && (file.size > 2097152 || !/\.(png|jpe?g)$/i.test(file.name)) ? 'Choose a JPEG or PNG image no larger than 2 MB.' : '');
        input.reportValidity();
    });
});

