$(document).ready(function () {
    // Константы
    const TRANSLATION_DELAY = 1000;
    const SCROLL_SYNC_DELAY = 5;

    // Состояние приложения
    const state = {
        isTranslationPaused: false,
        isTranslationCancelled: false,
        translationPingInterval: null,
        isSyncingOriginal: false,
        isSyncingTranslated: false
    };

    // Инициализация SignalR
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/translationHub")
        .withAutomaticReconnect()
        .build();

    // Обработчики событий SignalR
    connection.on("ReceiveTranslation", handleTranslationUpdate);
    connection.on("ReceiveOriginal", handleOriginalUpdate);
    connection.on("UpdateProgress", function (translationId, currentProgress, totalBlocks) {
        const progressPercent = Math.round((currentProgress / totalBlocks) * 100);
        updateProgress(progressPercent);
    });
    connection.on("TranslationStatusChanged", handleTranslationStatusChange);
    connection.on("NewTranslationIssue", function (issue) {
        console.log(`Новая проблема перевода в блоке ${issue.blockNumber}`);
    });

    connection.on("VerificationStatusUpdate", function (translationId, stepName, stepDescription, stepPercentage) {
        updateVerificationStatusPanel(stepName, stepDescription, stepPercentage);
    });

    connection.on("VerificationError", function (errorMessage) {
        console.error('Ошибка верификации:', errorMessage);

        const notification = $(`<div class="alert alert-danger alert-dismissible fade show" role="alert">
            Ошибка при проверке перевода: ${errorMessage}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>`);

        $('body').append(notification);
        setTimeout(() => {
            notification.alert('close');
        }, 5000);
    });

    connection.on("TranslationForApproval", function (data) {
        const issue = data.issue;
        const contextBefore = data.contextBefore || [];
        const contextAfter = data.contextAfter || [];

        $('#originalText').text(issue.originalText);
        $('#previousTranslation').text(issue.currentTranslation);
        $('#newTranslation').text(issue.improvedTranslation);
        $('#manualTranslation').val(issue.improvedTranslation);
        $('#issuesList').html(issue.problemTypes.map(type => `<span class="badge bg-warning me-1">${type}</span>`).join(''));

        let contextBeforeHtml = '';
        for (const block of contextBefore) {
            contextBeforeHtml += `<div class="context-block mb-2">
                <div class="text-muted small">Блок ${block.number}</div>
                <div class="border-start border-info ps-2">${block.text}</div>
            </div>`;
        }
        $('#contextBefore').html(contextBeforeHtml || '<div class="text-muted fst-italic">нет контекста</div>');

        let contextAfterHtml = '';
        for (const block of contextAfter) {
            contextAfterHtml += `<div class="context-block mb-2">
                <div class="text-muted small">Блок ${block.number}</div>
                <div class="border-start border-info ps-2">${block.text}</div>
            </div>`;
        }
        $('#contextAfter').html(contextAfterHtml || '<div class="text-muted fst-italic">нет контекста</div>');

        $('#editTranslationForm').addClass('d-none');
        $('#editTranslationBtn').removeClass('d-none');

        const modal = new bootstrap.Modal('#translationApprovalModal');
        modal.show();

        $('#approveTranslation').off('click').on('click', function () {
            connection.invoke("ApproveTranslation", issue.blockNumber, issue.improvedTranslation,
                TranslationIssueStatus.Approved);
            modal.hide();
        });

        $('#skipTranslation').off('click').on('click', function () {
            connection.invoke("ApproveTranslation", issue.blockNumber, issue.currentTranslation,
                TranslationIssueStatus.Skipped);
            modal.hide();
        });

        $('#editTranslationBtn').off('click').on('click', function () {
            $('#editTranslationForm').removeClass('d-none');
            $(this).addClass('d-none');
        });

        $('#saveManualTranslation').off('click').on('click', function () {
            const manualTranslation = $('#manualTranslation').val();
            issue.manualTranslation = manualTranslation;
            connection.invoke("ApproveTranslation", issue.blockNumber, manualTranslation,
                TranslationIssueStatus.ManuallyEdited);
            $('#editTranslationForm').addClass('d-none');
            $('#editTranslationBtn').removeClass('d-none');
            modal.hide();
        });

        $('#cancelManualTranslation').off('click').on('click', function () {
            $('#editTranslationForm').addClass('d-none');
            $('#editTranslationBtn').removeClass('d-none');
        });
    });

    connection.on("TranslationApproved", function (blockNumber, translation, status) {
        console.log(`Блок ${blockNumber} одобрен со статусом: ${status}`);
        updateSubtitleBlock('translatedSubtitle', blockNumber, translation);

        let statusText = "одобрен";
        let alertClass = "success";

        if (status === TranslationIssueStatus.Skipped) {
            statusText = "пропущен";
            alertClass = "info";
        } else if (status === TranslationIssueStatus.ManuallyEdited) {
            statusText = "отредактирован вручную";
            alertClass = "primary";
        }

        const notification = $(`<div class="alert alert-${alertClass} alert-dismissible fade show" role="alert">
            Блок ${blockNumber} ${statusText}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>`);

        $('body').append(notification);
        setTimeout(() => {
            notification.alert('close');
        }, 3000);
    });

    connection.on("TranslationRejected", function (blockNumber, translation) {
        console.log(`Блок ${blockNumber} отправлен на повторный перевод`);

        const notification = $(`<div class="alert alert-warning alert-dismissible fade show" role="alert">
            Блок ${blockNumber} отправлен на повторный перевод
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>`);

        $('body').append(notification);
        setTimeout(() => {
            notification.alert('close');
        }, 3000);
    });

    // Инициализация соединения
    connection.start()
        .then(() => console.log('SignalR Connected'))
        .catch(err => console.error('SignalR Connection Error: ', err));

    // Определение констант для перечисления статусов
    const TranslationIssueStatus = {
        Pending: 0,
        Approved: 1,
        Rejected: 2,
        ManuallyEdited: 3,
        Skipped: 4
    };

    // Обработчики событий формы
    initializeFormHandlers();
    initializeTranslationControls();
    initializeScrollSync();

    // Функции инициализации
    function initializeFormHandlers() {
        $('#translationForm').on('submit', handleFormSubmit);
    }

    function initializeTranslationControls() {
        $('#pauseTranslationBtn').on('click', handlePauseButton);
        $('#cancelTranslationBtn').on('click', handleCancelButton);
    }

    function initializeScrollSync() {
        $('#originalSubtitles').off('scroll').on('scroll', function () {
            if (!state.isSyncingOriginal) {
                state.isSyncingTranslated = true;
                $('#translatedSubtitles').scrollTop($(this).scrollTop());
                setTimeout(() => state.isSyncingTranslated = false, SCROLL_SYNC_DELAY);
            }
        });

        $('#translatedSubtitles').off('scroll').on('scroll', function () {
            if (!state.isSyncingTranslated) {
                state.isSyncingOriginal = true;
                $('#originalSubtitles').scrollTop($(this).scrollTop());
                setTimeout(() => state.isSyncingOriginal = false, SCROLL_SYNC_DELAY);
            }
        });
    }

    // Обработчики событий
    function handleFormSubmit(e) {
        e.preventDefault();
        const formData = new FormData(this);

        resetAllTranslationElements();

        connection.on("ReceiveTranslation", handleTranslationUpdate);
        connection.on("ReceiveOriginal", handleOriginalUpdate);
        connection.on("UpdateProgress", function (translationId, currentProgress, totalBlocks) {
            const progressPercent = Math.round((currentProgress / totalBlocks) * 100);
            updateProgress(progressPercent);
        });

        updateUIForTranslationStart();
        formData.append('translationId', generateTranslationId());

        updateProgress(0);

        $.ajax({
            url: $(this).attr('action') || window.location.href,
            type: 'POST',
            data: formData,
            contentType: false,
            processData: false,
            xhr: configureXHR,
            success: handleTranslationSuccess,
            error: handleTranslationError,
            complete: handleTranslationComplete
        });

        startTranslationPing(formData.get('translationId'));
    }

    function handlePauseButton() {
        state.isTranslationPaused = !state.isTranslationPaused;
        updatePauseButtonUI();
        updateTranslationStatus();
    }

    function handleCancelButton() {
        if (confirm('Вы уверены, что хотите отменить перевод?')) {
            state.isTranslationCancelled = true;

            connection.off("ReceiveTranslation");
            connection.off("ReceiveOriginal");
            connection.off("UpdateProgress");

            $('#resultsContainer').hide();
            $('#originalSubtitles, #translatedSubtitles').empty();

            $('#progressContainer, #translationControls').hide();

            updateTranslationStatus();

            updateProgress(0);

            if (state.translationPingInterval) {
                clearInterval(state.translationPingInterval);
                state.translationPingInterval = null;
            }

            resetAllTranslationElements();

            $('#resultsContainer').css('visibility', 'hidden');
        }
    }

    function handleTranslationUpdate(blockNumber, translation) {
        if (checkCancellationAndAbort()) {
            connection.off("ReceiveTranslation");
            connection.off("ReceiveOriginal");
            return;
        }
        updateSubtitleBlock('translatedSubtitle', blockNumber, translation);
    }

    function handleOriginalUpdate(blockNumber, originalText) {
        if (checkCancellationAndAbort()) {
            connection.off("ReceiveTranslation");
            connection.off("ReceiveOriginal");
            return;
        }
        updateSubtitleBlock('originalSubtitle', blockNumber, originalText);
    }

    function handleTranslationStatusChange(translationId, isPaused, isCancelled) {
        if (isCancelled) {
            connection.off("ReceiveTranslation");
            connection.off("ReceiveOriginal");
            connection.off("UpdateProgress");

            $('#resultsContainer').hide().css('visibility', 'hidden');
            $('#progressContainer, #translationControls').hide();
            $('#originalSubtitles, #translatedSubtitles').empty();

            state.isTranslationCancelled = true;

            resetAllTranslationElements();
        }
    }

    // Вспомогательные функции
    function resetTranslationState() {
        state.isTranslationPaused = false;
        state.isTranslationCancelled = false;
    }

    function updateUIForTranslationStart() {
        $('#translationForm button[type="submit"]')
            .prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Translating...');
        $('#progressContainer').show();
        $('#translationControls').show();
        updateProgress(0);
        $('#originalSubtitles').empty();
        $('#translatedSubtitles').empty();
    }

    function configureXHR() {
        const xhr = new window.XMLHttpRequest();
        xhr.upload.addEventListener('progress', function (evt) {
            if (evt.lengthComputable && !state.isTranslationCancelled) {
                const percentComplete = Math.round((evt.loaded / evt.total) * 100 * 0.1);
                updateProgress(percentComplete);
            }
        }, false);
        return xhr;
    }

    function handleTranslationSuccess(response, status, xhr) {
        if (state.isTranslationCancelled) return;

        setTimeout(() => {
            if (!state.isTranslationCancelled) {
                updateProgress(60);
                const contentType = xhr.getResponseHeader('Content-Type');

                if (contentType.includes('video/mp4')) {
                    handleVideoSuccess();
                } else {
                    handleSrtSuccess(response);
                }
            }
        }, 100);
    }

    function handleVideoSuccess() {
        updateProgress(100);
        $('#resultsContainer').hide();
        alert('Video with translated subtitles downloaded successfully!');
    }

    function handleSrtSuccess(response) {
        if (!state.isTranslationCancelled) {
            $('#resultsContainer').show();
            setTimeout(() => {
                if (!state.isTranslationCancelled) {
                    updateProgress(100);
                    appendDownloadButton(response);

                    const verificationAlert = $(`
                        <div class="alert alert-success alert-dismissible fade show mt-3" role="alert">
                            <h5 class="alert-heading">Перевод успешно завершен!</h5>
                            <p>Вы можете скачать переведенные субтитры или выполнить дополнительную проверку перевода с помощью искусственного интеллекта.</p>
                            <p>ИИ проанализирует перевод и предложит исправления для проблемных мест. Вам будет предложено просмотреть и подтвердить или отклонить каждое исправление.</p>
                            <p>Это экспериментальная опция, и иногда альтернативный перевод может оказаться менее подходящим по стилю и смыслу</p>
                            <hr>
                            <div class="d-flex justify-content-end">
                                <button class="btn btn-outline-primary me-2" id="verifyTranslationBtn">
                                    <i class="bi bi-check-circle"></i> Проверить перевод
                                </button>
                            </div>
                            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                        </div>
                    `);

                    $('#resultsContainer').append(verificationAlert);

                    $('#verifyTranslationBtn').on('click', function () {
                        const formData = new FormData();
                        formData.append('translationId', $('form').data('translationId'));

                        updateVerificationStatusPanel(
                            "Инициализация проверки",
                            "Подготовка к анализу перевода...",
                            5
                        );

                        verificationAlert.html(`
                            <div class="alert-heading">
                                <div class="d-flex align-items-center">
                                    <div class="spinner-border spinner-border-sm me-2" role="status">
                                        <span class="visually-hidden">Проверка...</span>
                                    </div>
                                    <h5 class="mb-0">Выполняется проверка перевода...</h5>
                                </div>
                            </div>
                            <p>Пожалуйста, подождите. ИИ анализирует качество перевода.</p>
                        `);

                        $.ajax({
                            url: '/Index?handler=Verify',
                            type: 'POST',
                            data: formData,
                            contentType: false,
                            processData: false,
                            headers: {
                                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                            },
                            success: function (response) {
                                if (response && response.issues && response.issues.length > 0) {
                                    verificationAlert.html(`
                                        <div class="alert-heading">
                                            <h5>Проверка перевода выполнена</h5>
                                        </div>
                                        <p>Найдено ${response.issues.length} проблем в переводе. Сейчас вам будут показаны возможные улучшения текста.</p>
                                        <p><strong>Каждое улучшение требует вашего подтверждения.</strong></p>
                                    `);
                                } else {
                                    verificationAlert.html(`
                                        <div class="alert-heading">
                                            <h5>Проверка перевода выполнена</h5>
                                        </div>
                                        <p>В переводе не обнаружено существенных проблем. Поздравляем!</p>
                                        <p>Вы можете скачать проверенный файл субтитров.</p>
                                    `);
                                }

                                // Создаем Blob из ответа и добавляем кнопку скачивания
                                const blob = new Blob([response], {type: 'text/plain'});
                                const url = URL.createObjectURL(blob);

                                if ($('#downloadVerifiedSubtitlesBtn').length === 0) {
                                    verificationAlert.append(`
                                        <hr>
                                        <div class="text-center">
                                            <a href="${url}" class="btn btn-primary" id="downloadVerifiedSubtitlesBtn" download="verified.srt">
                                                <i class="bi bi-download"></i> Скачать проверенные субтитры
                                            </a>
                                        </div>
                                    `);
                                }
                            },
                            error: function (error) {
                                console.error('Verification error:', error);

                                verificationAlert.html(`
                                    <div class="alert-heading">
                                        <h5>Ошибка проверки перевода</h5>
                                    </div>
                                    <p>При проверке произошла ошибка: ${error.statusText || 'Неизвестная ошибка'}</p>
                                    <p>Пожалуйста, попробуйте снова или скачайте текущую версию перевода.</p>
                                `);

                                $('#verificationStatusPanel').fadeOut('fast', function () {
                                    $(this).remove();
                                });
                            }
                        });
                    });
                }
            }, 100);
        }
    }

    function handleTranslationError(xhr, status, error) {
        if (state.isTranslationCancelled) {
            console.log('Translation cancelled by user');
            updateProgress(0);
            hideTranslationElements();
        } else {
            console.error('Translation error:', error);
            alert('Error processing request: ' + error);
            setTimeout(() => {
                if (!state.isTranslationCancelled) {
                    updateProgress(0);
                }
            }, 100);
        }
    }

    function handleTranslationComplete() {
        $('#translationForm button[type="submit"]')
            .prop('disabled', false)
            .html('Перевести');
        if (state.translationPingInterval) {
            clearInterval(state.translationPingInterval);
            state.translationPingInterval = null;
        }
    }

    function updatePauseButtonUI() {
        const $button = $('#pauseTranslationBtn');
        if (state.isTranslationPaused) {
            $button.html('<i class="bi bi-play-fill"></i> Продолжить');
            $('#progressBar').removeClass('progress-bar-animated');
        } else {
            $button.html('<i class="bi bi-pause-fill"></i> Пауза');
            $('#progressBar').addClass('progress-bar-animated');
        }
    }

    function updateTranslationStatus() {
        connection.invoke("UpdateTranslationStatus", $('form').data('translationId'),
            state.isTranslationPaused, state.isTranslationCancelled)
            .catch(err => console.error(err.toString()));
    }

    function updateSubtitleBlock(prefix, blockNumber, content) {
        if (checkCancellationAndAbort()) return;

        const blockId = `${prefix}-${blockNumber}`;

        if ($('#resultsContainer').css('display') === 'none') {
            if (checkCancellationAndAbort()) return;
            $('#resultsContainer').show().css('visibility', 'visible');
        }

        if (checkCancellationAndAbort()) return;

        const isNewBlock = $(`#${blockId}`).length === 0;
        if (isNewBlock) {
            $(`#${prefix}s`).append(
                `<div id="${blockId}" class="subtitle-block" data-block-number="${blockNumber}">
                    <div class="block-number">${blockNumber}</div>
                    <div class="translation">${content}</div>
                </div>`
            );
        } else {
            $(`#${blockId}`).find('.translation').html(content);
        }

        if (!checkCancellationAndAbort()) {
            syncSubtitleBlockHeights(blockNumber);
        }
    }

    function syncSubtitleBlockHeights(blockNumber) {
        const originalBlock = $('#originalSubtitle-' + blockNumber);
        const translatedBlock = $('#translatedSubtitle-' + blockNumber);
        if (originalBlock.length && translatedBlock.length) {
            const originalHeight = originalBlock.outerHeight();
            const translatedHeight = translatedBlock.outerHeight();
            const maxHeight = Math.max(originalHeight, translatedHeight);
            originalBlock.height(maxHeight);
            translatedBlock.height(maxHeight);
        }
    }

    function updateProgress(percent) {
        if (state.isTranslationCancelled) {
            percent = 0;
        }

        const $progressBar = $('#progressBar');

        percent = Math.max(0, Math.min(100, Math.round(percent)));

        if ($progressBar.attr('aria-valuenow') !== percent.toString()) {
            $progressBar.css('width', percent + '%')
                .attr('aria-valuenow', percent)
                .text(percent + '%');

            if (percent === 100) {
                $progressBar.addClass('bg-success');
                setTimeout(() => {
                    if (!state.isTranslationCancelled) {
                        $('#progressContainer').fadeOut(500);
                        $('#translationControls').fadeOut(500);
                    }
                }, 1500);
            } else {
                $progressBar.removeClass('bg-success');
            }
        }
    }

    function hideTranslationElements() {
        $('#progressContainer, #translationControls, #resultsContainer').stop(true, true);

        $('#progressContainer, #translationControls, #resultsContainer').hide();

        $('#originalSubtitles, #translatedSubtitles').empty();

        $('#resultsContainer').css('visibility', 'hidden');

        resetAllTranslationElements();
    }

    function resetAllTranslationElements() {
        $('#progressContainer, #translationControls, #resultsContainer').hide();
        $('#resultsContainer').css('visibility', 'hidden');

        $('#originalSubtitles, #translatedSubtitles').empty();

        $('#progressBar')
            .css('width', '0%')
            .attr('aria-valuenow', '0')
            .text('0%')
            .removeClass('bg-success');

        resetTranslationState();

        connection.off("ReceiveTranslation");
        connection.off("ReceiveOriginal");
        connection.off("UpdateProgress");
        connection.off("TranslationStatusChanged");

        connection.on("TranslationStatusChanged", handleTranslationStatusChange);

        setTimeout(() => {
            if (state.isTranslationCancelled) {
                $('#resultsContainer').hide().css('visibility', 'hidden');
            }
        }, 100);
    }

    function appendDownloadButton(response, filename = "translated.srt") {
        if ($('#downloadSubtitlesBtn').length === 0) {
            $('#resultsContainer').append(
                '<div class="mt-3 text-center">' +
                `<button id="downloadSubtitlesBtn" class="btn btn-primary">Скачать ${filename}</button>` +
                '</div>'
            );
            $('#downloadSubtitlesBtn').on('click', function () {
                const blob = new Blob([response], {type: 'text/plain'});
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = filename;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            });
        }
    }

    function startTranslationPing(translationId) {
        state.translationPingInterval = setInterval(function () {
            if (state.isTranslationPaused || state.isTranslationCancelled) {
                connection.invoke("UpdateTranslationStatus", translationId,
                    state.isTranslationPaused, state.isTranslationCancelled)
                    .catch(err => console.error(err.toString()));
            }
        }, TRANSLATION_DELAY);
    }

    function generateTranslationId() {
        const id = 'translation-' + new Date().getTime() + '-' + Math.floor(Math.random() * 1000);
        $('form').data('translationId', id);
        return id;
    }

    function checkCancellationAndAbort() {
        if (state.isTranslationCancelled) {
            $('#resultsContainer').hide().css('visibility', 'hidden');
            $('#progressContainer, #translationControls').hide();
            $('#originalSubtitles, #translatedSubtitles').empty();
            return true;
        }
        return false;
    }

    function updateVerificationStatusPanel(stepName, stepDescription, stepPercentage) {
        let statusPanel = $('#verificationStatusPanel');
        if (statusPanel.length === 0) {
            statusPanel = $(`
                <div id="verificationStatusPanel" class="card shadow-sm mb-3 verification-status-panel">
                    <div class="card-header bg-primary text-white d-flex justify-content-between align-items-center">
                        <h5 class="m-0">Статус проверки перевода</h5>
                        <button type="button" class="btn-close btn-close-white" aria-label="Close" id="closeVerificationStatus"></button>
                    </div>
                    <div class="card-body">
                        <div class="step-info mb-2">
                            <h6 id="currentStepName" class="fw-bold"></h6>
                            <p id="currentStepDescription" class="mb-2"></p>
                        </div>
                        <div class="progress" style="height: 20px;">
                            <div id="verificationProgressBar" class="progress-bar progress-bar-striped progress-bar-animated" 
                                 role="progressbar" style="width: 0%;" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100">0%</div>
                        </div>
                    </div>
                </div>
            `);

            $('#resultsContainer').before(statusPanel);

            $('#closeVerificationStatus').on('click', function () {
                $('#verificationStatusPanel').fadeOut('fast', function () {
                    $(this).remove();
                });
            });
        }

        $('#currentStepName').text(stepName);
        $('#currentStepDescription').text(stepDescription);

        const $progressBar = $('#verificationProgressBar');
        $progressBar.css('width', stepPercentage + '%')
            .attr('aria-valuenow', stepPercentage)
            .text(stepPercentage + '%');

        statusPanel.fadeIn('fast');
    }

    // Обработка antiforgery token для AJAX запросов
    $(document).ready(function () {
        $.ajaxSetup({
            headers: {
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
            }
        });
    });
});

