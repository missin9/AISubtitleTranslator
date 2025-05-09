# AISubtitleTranslator

## Описание проекта
AISubtitleTranslator - это веб-приложение для автоматического перевода субтитров в формате SRT с использованием API Mistral. Приложение позволяет переводить субтитры на различные языки, сохраняя при этом структуру временных меток и форматирование оригинального файла.

## Основные возможности
- Загрузка и перевод SRT-файлов субтитров
- Выбор языка перевода (русский, французский, немецкий)
- Настройка стиля перевода:
  - **Точный перевод** - сохраняет все детали и нюансы оригинала, идеален для технических текстов
  - **Естественный перевод** - адаптирует текст под особенности целевого языка, сохраняя смысл
  - **Креативный перевод** - сохраняет стиль и эмоциональную окраску оригинала
- Настройка размера контекста для перевода:
  - **Малый контекст** (2 блока) - для слабых моделей
  - **Средний контекст** (4 блока) - оптимален для большинства случаев
  - **Большой контекст** (8 блоков) - для лучшей связности перевода
- Возможность приостановки и отмены процесса перевода
- Интерактивный режим проверки и утверждения перевода
- Автоматическое определение и исправление проблем в переводе


### Запуск приложения
1. Убедитесь, что на вашем компьютере установлен .NET 9.0
2. Настройте API-ключ для Mistral AI в файле конфигурации `appsettings.json`
3. Запустите приложение с помощью команды `dotnet run` или через IDE

### Перевод субтитров
1. Откройте приложение в браузере (по умолчанию: http://localhost:5270)
2. Загрузите SRT-файл, используя форму на главной странице
3. Выберите целевой язык перевода
4. Настройте размер контекста и стиль перевода в соответствии с вашими потребностями
5. Нажмите кнопку "Перевести"
6. Во время перевода вы можете:
   - Отслеживать прогресс в реальном времени
   - Просматривать оригинальные и переведенные субтитры
   - При необходимости приостановить или отменить процесс

### Проверка и утверждение перевода
1. После завершения автоматического перевода система предложит выявить потенциальные проблемы
2. Для каждой проблемы вам будет предложено:
   - Просмотреть оригинальный текст и его перевод
   - Ознакомиться с контекстом до и после переводимого блока
   - Принять предложенный улучшенный перевод или отредактировать его вручную

### Сохранение результата
После завершения перевода и проверки, вы можете скачать готовый переведенный или исправленный после перевпроверки SRT-файл.

## Примечания
- В папке TestSubtitles лежит файл, на котором можно протестировать функционал приложения
- Для работы приложения требуется действующий API-ключ Mistral
- Качество перевода зависит от выбранной модели и параметров перевода
- Размер контекста влияет на скорость перевода и потребление токенов Mistral
