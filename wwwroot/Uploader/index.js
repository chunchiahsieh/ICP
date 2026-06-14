(function (global, $) {
'use strict';

if (!$) {
    return;
}
function formatBytes(bytes, decimals = 2) {
    if (!+bytes) return '0 Bytes';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
}

// Utility function for escaping HTML to prevent XSS
function escapeHtml(unsafe) {
    return (unsafe || '').toString()
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

window.createUploader = function (selector, options) {
    const defaultOptions = {
        title: '檔案上傳',
        buttonText: '開啟上傳',
        buttonClass: 'px-5 py-2.5 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium shadow-sm inline-flex justify-center items-center gap-2',
        uploadUrl: '',
        fileTypes: '*/*',
        multiple: true,
        maxSize: 10,
        maxSizeHint: '',
        fieldName: 'file',
        onSuccess: null,
        onError: null
    };
    const config = { ...defaultOptions, ...options };

    const $container = $(selector);

    // Render the initial button
    const buttonId = `open-modal-${Math.random().toString(36).substring(2, 9)}`;
    $container.html(`
    <button type="button" id="${buttonId}" class="${config.buttonClass}">
      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 14.899A7 7 0 1 1 15.71 8h1.79a4.5 4.5 0 0 1 2.5 8.242"/><path d="M12 12v9"/><path d="m16 16-4-4-4 4"/></svg>
      ${escapeHtml(config.buttonText)}
    </button>
  `);

    // Generate modal HTML
    const modalId = `modal-${Math.random().toString(36).substring(2, 9)}`;
    const dropzoneId = `dropzone-${modalId}`;
    const fileInputId = `file-input-${modalId}`;
    const fileListContainerId = `file-list-container-${modalId}`;
    const fileListId = `file-list-${modalId}`;
    const fileCountId = `file-count-${modalId}`;

    const modalHtml = `
    <div id="${modalId}" class="uploader-modal-root fixed inset-0 hidden">
      <div class="uploader-backdrop fixed inset-0 bg-slate-900/50 transition-opacity opacity-0"></div>
      <div class="uploader-modal-body fixed inset-0 w-screen overflow-y-auto pointer-events-none">
        <div class="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
          <div class="uploader-panel relative transform overflow-hidden rounded-2xl bg-white text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-xl opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95 pointer-events-auto">
            
            <!-- Header -->
            <div class="bg-white px-5 py-4 border-b border-slate-100 flex items-center justify-between">
              <h3 class="text-lg font-semibold leading-6 text-slate-900">${escapeHtml(config.title)}</h3>
              <button type="button" class="close-modal bg-white text-slate-400 hover:text-slate-500 rounded-lg p-1 transition-colors">
                <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" /></svg>
              </button>
            </div>

            <!-- Body -->
            <div class="bg-white px-5 pb-5 pt-6">
              <div class="w-full space-y-6">
                <!-- Dropzone -->
                <div id="${dropzoneId}" class="relative overflow-hidden border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-all duration-200 border-slate-300 bg-slate-50 hover:bg-slate-100 hover:border-slate-400 group">
                  <input type="file" id="${fileInputId}" class="hidden" ${config.multiple ? 'multiple' : ''} accept="${escapeHtml(config.fileTypes)}" />
                  <div class="flex flex-col items-center justify-center space-y-3 pointer-events-none">
                    <div class="dropzone-icon p-3 rounded-full transition-colors bg-white text-slate-500 shadow-sm group-hover:text-blue-500 group-hover:scale-110">
                      <svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 14.899A7 7 0 1 1 15.71 8h1.79a4.5 4.5 0 0 1 2.5 8.242"/><path d="M12 12v9"/><path d="m16 16-4-4-4 4"/></svg>
                    </div>
                    <div class="space-y-1">
                      <h3 class="text-base font-medium text-slate-800">點擊或拖曳檔案至此</h3>
                      <p class="text-xs text-slate-500">${config.multiple ? '支援多個檔案' : '僅支援單一檔案'}${config.fileTypes !== '*/*' ? ` (${escapeHtml(config.fileTypes)})` : ''}</p>
                      <p class="text-xs text-slate-500">${escapeHtml(config.maxSizeHint || ('單檔上限 ' + config.maxSize + 'MB'))}</p>
                    </div>
                  </div>
                </div>

                <!-- File List -->
                <div id="${fileListContainerId}" class="space-y-3 mt-6 hidden">
                  <h4 class="text-xs font-medium text-slate-500 flex items-center justify-between tracking-wider">
                    <div class="flex items-center gap-2">
                      <span class="uppercase">已選擇的檔案 (<span id="${fileCountId}">0</span>)</span>
                      <span id="success-wrapper-${modalId}" class="hidden text-emerald-600 bg-emerald-50 px-1.5 py-0.5 rounded normal-case">成功 <span id="success-count-${modalId}">0</span></span>
                      <span id="error-wrapper-${modalId}" class="hidden text-red-600 bg-red-50 px-1.5 py-0.5 rounded normal-case">失敗 <span id="error-count-${modalId}">0</span></span>
                    </div>
                  </h4>
                  <div id="${fileListId}" class="space-y-3 max-h-64 overflow-y-auto pr-1"></div>
                </div>
              </div>
            </div>
            
            <!-- Footer -->
            <div class="bg-slate-50 px-5 py-3 sm:flex sm:flex-row-reverse border-t border-slate-100">
              <button type="button" class="close-modal mt-3 inline-flex w-full justify-center rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50 sm:mt-0 sm:w-auto transition-colors">關閉</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `;

    $('body').append(modalHtml);

    const $modal = $(`#${modalId}`);
    const $backdrop = $modal.find('.uploader-backdrop');
    const $panel = $modal.find('.uploader-panel');
    const $dropzone = $(`#${dropzoneId}`);
    const $fileInput = $(`#${fileInputId}`);
    const $fileListContainer = $(`#${fileListContainerId}`);
    const $fileList = $(`#${fileListId}`);
    const $fileCount = $(`#${fileCountId}`);
    const $successWrapper = $(`#success-wrapper-${modalId}`);
    const $successCount = $(`#success-count-${modalId}`);
    const $errorWrapper = $(`#error-wrapper-${modalId}`);
    const $errorCount = $(`#error-count-${modalId}`);

    let fileCount = 0;
    let successCount = 0;
    let errorCount = 0;

    function openModal() {
        $modal.removeClass('hidden');
        void $modal[0].offsetWidth; // trigger reflow
        $backdrop.removeClass('opacity-0').addClass('opacity-100');
        $panel.removeClass('opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95').addClass('opacity-100 translate-y-0 sm:scale-100');
    }

    function closeModal() {
        $backdrop.removeClass('opacity-100').addClass('opacity-0');
        $panel.removeClass('opacity-100 translate-y-0 sm:scale-100').addClass('opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95');
        setTimeout(() => {
            $modal.addClass('hidden');
        }, 300);
    }

    $(`#${buttonId}`).on('click', openModal);
    $modal.find('.close-modal').on('click', closeModal);
    $backdrop.on('click', closeModal);

    // Dropzone Events
    $dropzone.on('click', function () {
        $fileInput.trigger('click');
    });

    $dropzone.on('dragenter dragover dragleave drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
    });

    $dropzone.on('dragenter dragover', function () {
        $dropzone.removeClass('border-slate-300 bg-slate-50 hover:bg-slate-100 hover:border-slate-400')
            .addClass('border-blue-500 bg-blue-50/50 scale-[1.02]');
        $dropzone.find('.dropzone-icon').removeClass('bg-white text-slate-500 shadow-sm').addClass('bg-blue-100 text-blue-600');
    });

    $dropzone.on('dragleave drop', function () {
        $dropzone.removeClass('border-blue-500 bg-blue-50/50 scale-[1.02]')
            .addClass('border-slate-300 bg-slate-50 hover:bg-slate-100 hover:border-slate-400');
        $dropzone.find('.dropzone-icon').removeClass('bg-blue-100 text-blue-600').addClass('bg-white text-slate-500 shadow-sm');
    });

    $dropzone.on('drop', function (e) {
        const dt = e.originalEvent?.dataTransfer;
        if (dt && dt.files && dt.files.length > 0) {
            handleFiles(Array.from(dt.files));
        }
    });

    $fileInput.on('change', function (e) {
        const target = e.target;
        if (target.files && target.files.length > 0) {
            handleFiles(Array.from(target.files));
        }
        $(this).val('');
    });

    function handleFiles(files) {
        if (files.length === 0) return;

        // Check multiple handling
        if (!config.multiple && files.length > 1) {
            files = [files[0]];
        }

        // Clear existing for single select
        if (!config.multiple) {
            $fileList.empty();
            fileCount = 0;
            successCount = 0;
            errorCount = 0;
        }

        $fileListContainer.removeClass('hidden');

        files.forEach(file => {
            let clientError = null;

            // Size validation
            if (file.size > config.maxSize * 1024 * 1024) {
                clientError = `檔案超過 ${config.maxSize}MB`;
            }

            // Basic client side validation via fileTypes
            if (!clientError && config.fileTypes && config.fileTypes !== '*/*') {
                const acceptedTypes = config.fileTypes.split(',').map(s => s.trim().toLowerCase());
                const fileExt = '.' + file.name.split('.').pop().toLowerCase();
                let isAccepted = false;

                for (const type of acceptedTypes) {
                    if (type.startsWith('.')) {
                        if (fileExt === type) isAccepted = true;
                    } else if (type.endsWith('/*')) {
                        const mimeGroup = type.split('/')[0];
                        if (file.type.startsWith(mimeGroup + '/')) isAccepted = true;
                    } else {
                        if (file.type === type) isAccepted = true;
                    }
                }

                if (!isAccepted) {
                    clientError = `❌ 不支援的檔案格式`;
                }
            }

            fileCount++;
            updateFileCount();
            uploadFile(file, clientError);
        });
    }

    function updateFileCount() {
        $fileCount.text(fileCount);

        $successCount.text(successCount);
        if (successCount > 0) $successWrapper.removeClass('hidden');
        else $successWrapper.addClass('hidden');

        $errorCount.text(errorCount);
        if (errorCount > 0) $errorWrapper.removeClass('hidden');
        else $errorWrapper.addClass('hidden');

        if (fileCount === 0) {
            $fileListContainer.addClass('hidden');
        }
    }

    function uploadFile(file, clientError) {
        const id = Math.random().toString(36).substring(2, 9);
        const fileSizeStr = formatBytes(file.size);
        const safeFileName = escapeHtml(file.name);

        const $fileItem = $(`
      <div id="file-${id}" data-status="uploading" class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm flex items-start gap-4 opacity-0 scale-95 transition-all duration-300 relative group overflow-hidden">
        <div class="shrink-0 p-2 bg-slate-50 rounded-lg">
          <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="text-slate-400"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><path d="M10 9H8"/><path d="M16 13H8"/><path d="M16 17H8"/></svg>
        </div>
        
        <div class="flex-1 min-w-0 pr-6">
          <p class="text-sm font-medium text-slate-900 truncate mb-1" title="${safeFileName}">${safeFileName}</p>
          
          <div class="status-text flex items-center gap-2 text-xs text-slate-500 mb-2">
            <span>${fileSizeStr}</span>
            <span>&bull;</span>
            <span class="text-blue-600 flex items-center gap-1">
              <svg class="animate-spin w-3 h-3" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
              上傳中... <span class="progress-percent">0</span>%
            </span>
          </div>

          <div class="relative h-1.5 w-full bg-slate-100 rounded-full overflow-hidden">
             <div class="progress-bar absolute top-0 left-0 h-full bg-blue-500 transition-all duration-200" style="width: 0%;"></div>
          </div>
        </div>
        
        <button class="remove-btn absolute top-3 right-3 shrink-0 text-slate-400 hover:text-red-500 transition-colors p-1 bg-white" title="移除檔案">
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>
        </button>
      </div>
    `);

        $fileList.prepend($fileItem);

        setTimeout(() => {
            $fileItem.removeClass('opacity-0 scale-95').addClass('opacity-100 scale-100');
        }, 10);

        let jqXHR = null;

        $fileItem.find('.remove-btn').on('click', function (e) {
            e.stopPropagation();
            if (jqXHR && jqXHR.readyState !== 4) {
                jqXHR.abort();
            }

            const status = $fileItem.attr('data-status');
            if (status === 'success') successCount--;
            if (status === 'error') errorCount--;

            $fileItem.removeClass('opacity-100 scale-100').addClass('opacity-0 scale-95');
            setTimeout(() => {
                $fileItem.remove();
                fileCount--;
                updateFileCount();
            }, 300);
        });

        if (clientError) {
            $fileItem.attr('data-status', 'error');
            errorCount++;
            updateFileCount();
            markError(clientError);
            if (config.onError) config.onError(clientError);
            return;
        }

        const formData = new FormData();
        formData.append(config.fieldName, file);

        jqXHR = $.ajax({
            url: config.uploadUrl,
            type: 'POST',
            data: formData,
            dataType: 'json',
            processData: false,
            contentType: false,
            xhr: function () {
                const xhr = new window.XMLHttpRequest();
                xhr.upload.addEventListener("progress", function (evt) {
                    if (evt.lengthComputable) {
                        const percentComplete = Math.floor((evt.loaded / evt.total) * 100);
                        $fileItem.find('.progress-bar').css('width', `${percentComplete}%`);
                        $fileItem.find('.progress-percent').text(percentComplete);
                    }
                }, false);
                return xhr;
            },
            success: function (response) {
                $fileItem.find('.progress-bar').css('width', '100%');
                if (response.success) {
                    $fileItem.attr('data-status', 'success');
                    successCount++;
                    updateFileCount();

                    $fileItem.find('.progress-bar').removeClass('bg-blue-500').addClass('bg-emerald-500');
                    $fileItem.find('.status-text').html(`
                    <span>${fileSizeStr}</span>
                    <span>&bull;</span>
                    <span class="text-emerald-600 flex items-center gap-1">
                    <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z"/><path d="m9 12 2 2 4-4"/></svg>
                    ${escapeHtml(response.message)}
                    </span>
                `);

                    if (config.onSuccess) config.onSuccess(response);
                } else {
                    $fileItem.attr('data-status', 'error');
                    errorCount++;
                    updateFileCount();

                    markError(response.message || '上傳失敗');
                    if (config.onError) config.onError(response.message || '上傳失敗');
                }
            },
            error: function (xhr, status, error) {
                if (status === 'abort') {
                    return; // User aborted
                }

                $fileItem.attr('data-status', 'error');
                errorCount++;
                updateFileCount();

                let errorMessage = '連線失敗或伺服器錯誤';
                try {
                    if (xhr.responseJSON && xhr.responseJSON.message) {
                        errorMessage = xhr.responseJSON.message;
                    }
                } catch (e) { }
                markError(errorMessage);
                if (config.onError) config.onError(errorMessage);
            }
        });

        function markError(errorMsg) {
            $fileItem.removeClass('border-slate-200').addClass('border-red-200');
            $fileItem.find('.progress-bar').removeClass('bg-blue-500 bg-emerald-500').addClass('bg-red-500');
            $fileItem.find('.status-text').html(`
            <span>${fileSizeStr}</span>
            <span>&bull;</span>
            <span class="text-red-600 flex items-center gap-1">
                <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" x2="12" y1="8" y2="12"/><line x1="12" x2="12.01" y1="16" y2="16"/></svg>
                ${escapeHtml(errorMsg)}
            </span>
        `);
        }
    }
};

})(window, window.jQuery);

