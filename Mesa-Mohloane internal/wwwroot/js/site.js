// Mesa-Mohloane Global JavaScript

document.addEventListener('DOMContentLoaded', function () {
    // Hide preloader after page load
    const preloader = document.getElementById('global-preloader');
    if (preloader) {
        setTimeout(() => {
            preloader.classList.add('fade-out');
        }, 400);
    }

    // Re-init Lucide icons (for dynamically loaded content)
    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }
});

// Global Loader Utility
window.MesaLoader = {
    show: function () {
        const preloader = document.getElementById('global-preloader');
        if (preloader) {
            preloader.classList.remove('fade-out');
        }
    },
    hide: function () {
        const preloader = document.getElementById('global-preloader');
        if (preloader) {
            preloader.classList.add('fade-out');
        }
    }
};
