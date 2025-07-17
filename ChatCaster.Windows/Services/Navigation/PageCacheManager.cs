using System.Windows.Controls;
using ChatCaster.Windows.ViewModels.Navigation;
using Serilog;

namespace ChatCaster.Windows.Services.Navigation
{
    /// <summary>
    /// Менеджер кеша страниц
    /// Отвечает за кеширование и получение уже созданных страниц
    /// </summary>
    public class PageCacheManager
    {
        private readonly Dictionary<string, Page> _cachedPages = new();

        /// <summary>
        /// Получает страницу из кеша или создает новую через фабрику
        /// </summary>
        public Page GetOrCreatePage(string pageTag, Func<string, Page> pageCreator)
        {
            if (string.IsNullOrEmpty(pageTag))
            {
                return GetMainPageOrDefault();
            }

            // Проверяем кеш
            if (_cachedPages.TryGetValue(pageTag, out var cachedPage))
            {
                return cachedPage;
            }

            // Создаем новую страницу
            try
            {
                var newPage = pageCreator(pageTag);
                
                // Кешируем созданную страницу
                _cachedPages[pageTag] = newPage;
                
                return newPage;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PageCacheManager: ошибка создания страницы {PageTag}", pageTag);
                return GetMainPageOrDefault();
            }
        }

        /// <summary>
        /// Добавляет страницу в кеш (для MainPage которая создается отдельно)
        /// </summary>
        public void CachePage(string pageTag, Page page)
        {
            if (string.IsNullOrEmpty(pageTag))
            {
                return;
            }

            _cachedPages[pageTag] = page;
        }

        /// <summary>
        /// Получает страницу из кеша без создания новой
        /// </summary>
        public Page? GetCachedPage(string pageTag)
        {
            _cachedPages.TryGetValue(pageTag, out var page);
            return page;
        }

        /// <summary>
        /// Получает все закешированные страницы для cleanup
        /// </summary>
        public Dictionary<string, Page> GetAllCachedPages()
        {
            return new Dictionary<string, Page>(_cachedPages);
        }

        /// <summary>
        /// Очищает весь кеш страниц
        /// </summary>
        public void ClearCache()
        {
            try
            {
                var pageCount = _cachedPages.Count;
                _cachedPages.Clear();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PageCacheManager: ошибка очистки кеша");
            }
        }

        /// <summary>
        /// Проверяет, есть ли страница в кеше
        /// </summary>
        public bool IsPageCached(string pageTag)
        {
            return !string.IsNullOrEmpty(pageTag) && _cachedPages.ContainsKey(pageTag);
        }

        /// <summary>
        /// Получает количество закешированных страниц
        /// </summary>
        public int CachedPageCount => _cachedPages.Count;

        private Page GetMainPageOrDefault()
        {
            // Пытаемся вернуть MainPage из кеша
            if (_cachedPages.TryGetValue(NavigationConstants.MainPage, out var mainPage))
            {
                return mainPage;
            }
            throw new InvalidOperationException("MainPage должна быть всегда доступна в кеше");
        }
    }
}