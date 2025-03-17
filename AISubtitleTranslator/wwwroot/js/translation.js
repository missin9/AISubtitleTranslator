// Обработка выбора стиля перевода
document.addEventListener('DOMContentLoaded', function () {
    const translationStyleSelect = document.getElementById('TranslationStyle');
    if (translationStyleSelect) {
        translationStyleSelect.addEventListener('change', function () {
            const selectedStyle = this.value;
            updateStyleDescription(selectedStyle);
        });
    }

    const contextSizeSelect = document.getElementById('ContextSize');
    if (contextSizeSelect) {
        contextSizeSelect.addEventListener('change', function () {
            const selectedSize = this.value;
            updateContextSizeDescription(selectedSize);
        });
    }
});

// Обновление описания выбранного стиля
function updateStyleDescription(style) {
    const descriptions = {
        'Precise': 'Точный перевод с сохранением всех деталей и нюансов оригинала. Идеально подходит для технических текстов и документации.',
        'Natural': 'Естественный перевод с адаптацией под русский язык. Сохраняет смысл, но использует привычные для русскоязычного читателя конструкции.',
        'Creative': 'Креативный перевод с сохранением стиля и эмоциональной окраски оригинала. Подходит для художественных текстов и развлекательного контента.'
    };

    // Создаем или обновляем элемент с описанием
    let descriptionElement = document.getElementById('styleDescription');
    if (!descriptionElement) {
        descriptionElement = document.createElement('div');
        descriptionElement.id = 'styleDescription';
        descriptionElement.className = 'alert alert-info mt-2';
        const styleSelect = document.getElementById('TranslationStyle');
        styleSelect.parentNode.appendChild(descriptionElement);
    }

    descriptionElement.textContent = descriptions[style];
}

// Обновление описания выбранного размера контекста
function updateContextSizeDescription(size) {
    const descriptions = {
        'small': 'Малый контекст (2 переводимых блока) - перевод каждого блока с учетом только ближайшего соседнего блока. Подходит для слабых моделей, которые не могут учитывать больший контекст.',
        'medium': 'Средний контекст (4 переводимых блока) - Больше подходит для не очень сильных моделей которые развернуты локально в Ollama. Учитывает 2 блока до и 3 блока после переводимого текста.',
        'large': 'Большой контекст (8 переводимых блоков) - максимальный учет контекста для лучшего понимания смысла. Учитывает 4 блока до и 4 блока после. Может работать медленнее и быть дороже по токенам, но обеспечивает лучшую связность перевода.'
    };

    // Создаем или обновляем элемент с описанием
    let descriptionElement = document.getElementById('contextSizeDescription');
    if (!descriptionElement) {
        descriptionElement = document.createElement('div');
        descriptionElement.id = 'contextSizeDescription';
        descriptionElement.className = 'alert alert-info mt-2';
        const contextSizeSelect = document.getElementById('ContextSize');
        contextSizeSelect.parentNode.appendChild(descriptionElement);
    }

    descriptionElement.textContent = descriptions[size];
}

// Инициализация описаний при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    const translationStyleSelect = document.getElementById('TranslationStyle');
    if (translationStyleSelect) {
        updateStyleDescription(translationStyleSelect.value);
    }

    const contextSizeSelect = document.getElementById('ContextSize');
    if (contextSizeSelect) {
        updateContextSizeDescription(contextSizeSelect.value);
    }
}); 