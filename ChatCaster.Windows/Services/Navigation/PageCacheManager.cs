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
                Log.Warning("PageCacheManager: пустой pageTag, возвращаем MainPage");
                return GetMainPageOrDefault();
            }

            // Проверяем кеш
            if (_cachedPages.TryGetValue(pageTag, out var cachedPage))
            {
                Log.Debug("PageCacheManager: используем кешированную страницу: {PageTag}", pageTag);
                return cachedPage;
            }

            // Создаем новую страницу
            try
            {
                Log.Debug("PageCacheManager: создаем новую страницу: {PageTag}", pageTag);
                var newPage = pageCreator(pageTag);
                
                // Кешируем созданную страницу
                _cachedPages[pageTag] = newPage;
                
                Log.Debug("PageCacheManager: страница создана и закеширована: {PageTag}", pageTag);
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
            if (string.IsNullOrEmpty(pageTag) || page == null)
            {
                Log.Warning("PageCacheManager: некорректные параметры для кеширования");
                return;
            }

            _cachedPages[pageTag] = page;
            Log.Debug("PageCacheManager: страница добавлена в кеш: {PageTag}", pageTag);
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
                Log.Information("PageCacheManager: кеш очищен, удалено {Count} страниц", pageCount);
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

            Log.Error("PageCacheManager: MainPage не найдена в кеше, это критическая ошибка");
            throw new InvalidOperationException("MainPage должна быть всегда доступна в кеше");
        }
    }
}