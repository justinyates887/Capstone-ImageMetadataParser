// Function to download files (used for CSV export)
window.downloadFile = (filename, content) => {
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);

    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Clean up the URL
    URL.revokeObjectURL(url);
};

// Enhanced drag and drop functionality
window.setupDragDrop = (uploadAreaId, fileInputId, dotNetRef) => {
    const uploadArea = document.getElementById(uploadAreaId);
    const fileInput = document.getElementById(fileInputId);

    if (!uploadArea || !fileInput) return;

    // Prevent default drag behaviors
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        uploadArea.addEventListener(eventName, preventDefaults, false);
        document.body.addEventListener(eventName, preventDefaults, false);
    });

    // Highlight drop area when item is dragged over it
    ['dragenter', 'dragover'].forEach(eventName => {
        uploadArea.addEventListener(eventName, () => {
            uploadArea.classList.add('drag-over');
        }, false);
    });

    ['dragleave', 'drop'].forEach(eventName => {
        uploadArea.addEventListener(eventName, () => {
            uploadArea.classList.remove('drag-over');
        }, false);
    });

    // Handle dropped files
    uploadArea.addEventListener('drop', handleDrop, false);

    function preventDefaults(e) {
        e.preventDefault();
        e.stopPropagation();
    }

    function handleDrop(e) {
        const dt = e.dataTransfer;
        const files = dt.files;

        // Filter for image files only
        const imageFiles = Array.from(files).filter(file =>
            file.type.startsWith('image/') ||
            /\.(jpg|jpeg|png|tiff|tif|bmp|gif|webp|raw|cr2|nef|orf)$/i.test(file.name)
        );

        if (imageFiles.length > 0) {
            // Create a new DataTransfer object and add our filtered files
            const dataTransfer = new DataTransfer();
            imageFiles.forEach(file => dataTransfer.items.add(file));

            // Update the file input
            fileInput.files = dataTransfer.files;

            // Trigger the change event
            const event = new Event('change', { bubbles: true });
            fileInput.dispatchEvent(event);
        }
    }
};

// Function to show toast notifications
window.showToast = (message, type = 'info') => {
    // Create toast container if it doesn't exist
    let toastContainer = document.getElementById('toast-container');
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.id = 'toast-container';
        toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
        toastContainer.style.zIndex = '9999';
        document.body.appendChild(toastContainer);
    }

    // Create toast element
    const toastId = 'toast-' + Date.now();
    const toast = document.createElement('div');
    toast.id = toastId;
    toast.className = `toast align-items-center text-white bg-${type} border-0`;
    toast.setAttribute('role', 'alert');
    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                ${message}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
        </div>
    `;

    toastContainer.appendChild(toast);

    // Initialize and show the toast
    const bsToast = new bootstrap.Toast(toast, {
        autohide: true,
        delay: 5000
    });
    bsToast.show();

    // Remove the toast element after it's hidden
    toast.addEventListener('hidden.bs.toast', () => {
        toast.remove();
    });
};

// Function to copy text to clipboard
window.copyToClipboard = async (text) => {
    try {
        await navigator.clipboard.writeText(text);
        showToast('Copied to clipboard!', 'success');
        return true;
    } catch (err) {
        // Fallback for older browsers
        const textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.position = 'fixed';
        textArea.style.left = '-999999px';
        textArea.style.top = '-999999px';
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();

        try {
            document.execCommand('copy');
            showToast('Copied to clipboard!', 'success');
            return true;
        } catch (err) {
            showToast('Failed to copy to clipboard', 'danger');
            return false;
        } finally {
            textArea.remove();
        }
    }
};

// Function to format file sizes
window.formatFileSize = (bytes) => {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

// Function to validate image files
window.validateImageFile = (file, maxSizeBytes = 52428800) => { // 50MB default
    const validTypes = [
        'image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/bmp',
        'image/webp', 'image/svg+xml', 'image/tiff', 'image/tiff-fx',
        'image/x-icon', 'image/vnd.microsoft.icon', 'image/avif',
        'image/heic', 'image/heif'
    ];

    const validExtensions = [
        '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg',
        '.tiff', '.tif', '.ico', '.avif', '.heic', '.heif'
    ];

    // Check file type
    const isValidType = validTypes.includes(file.type) ||
        validExtensions.some(ext => file.name.toLowerCase().endsWith(ext));

    // Check file size
    const isValidSize = file.size <= maxSizeBytes;

    return {
        isValid: isValidType && isValidSize,
        errors: [
            ...(isValidType ? [] : ['Invalid file type. Please select a supported image file.']),
            ...(isValidSize ? [] : [`File too large. Maximum size is ${formatFileSize(maxSizeBytes)}.`])
        ]
    };
};

// Function to get file metadata without processing (for preview)
window.getFilePreviewInfo = (file) => {
    return new Promise((resolve) => {
        const reader = new FileReader();

        reader.onload = (e) => {
            const img = new Image();
            img.onload = () => {
                resolve({
                    name: file.name,
                    size: file.size,
                    type: file.type,
                    lastModified: new Date(file.lastModified),
                    dimensions: {
                        width: img.width,
                        height: img.height
                    }
                });
            };
            img.onerror = () => {
                resolve({
                    name: file.name,
                    size: file.size,
                    type: file.type,
                    lastModified: new Date(file.lastModified),
                    dimensions: null
                });
            };
            img.src = e.target.result;
        };

        reader.onerror = () => {
            resolve({
                name: file.name,
                size: file.size,
                type: file.type,
                lastModified: new Date(file.lastModified),
                dimensions: null
            });
        };

        reader.readAsDataURL(file);
    });
};

// Function to batch process files with progress callback
window.processFilesWithProgress = async (files, processFunction, progressCallback) => {
    const results = [];
    const total = files.length;

    for (let i = 0; i < files.length; i++) {
        try {
            const result = await processFunction(files[i]);
            results.push(result);

            if (progressCallback) {
                progressCallback({
                    current: i + 1,
                    total: total,
                    percentage: Math.round(((i + 1) / total) * 100),
                    currentFile: files[i].name
                });
            }
        } catch (error) {
            console.error(`Error processing file ${files[i].name}:`, error);
            results.push({ error: error.message, fileName: files[i].name });
        }
    }

    return results;
};

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    console.log('Image Metadata Parser - JavaScript helpers loaded');
});

// Export functions for module usage if needed
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        downloadFile: window.downloadFile,
        showToast: window.showToast,
        copyToClipboard: window.copyToClipboard,
        formatFileSize: window.formatFileSize,
        validateImageFile: window.validateImageFile,
        getFilePreviewInfo: window.getFilePreviewInfo,
        processFilesWithProgress: window.processFilesWithProgress
    };
}