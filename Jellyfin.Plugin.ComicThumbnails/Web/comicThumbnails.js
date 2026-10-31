/*
 * Comic Thumbnails - adds a "Pages" section with page thumbnails to the detail page of comics
 * in the Jellyfin web client. Clicking a thumbnail opens the comic reader at that page.
 *
 * Loaded by the plugin through a <script> tag in index.html.
 */
(function () {
    'use strict';

    if (window.__comicThumbnailsLoaded) {
        return;
    }
    window.__comicThumbnailsLoaded = true;

    var SECTION_CLASS = 'comicThumbnailsSection';
    // jellyfin-web's comic reader: page index = startPositionTicks / 10000.
    var TICKS_PER_PAGE = 10000;
    var MAX_PARALLEL_LOADS = 4;

    var TEXT = {
        en: {
            pages: 'Pages',
            page: 'Page',
            range: '{0}–{1} of {2}',
            first: 'First',
            previous: 'Previous',
            next: 'Next',
            last: 'Last',
            openFailed: 'The reader could not be opened at this page.',
            current: 'Last read'
        },
        de: {
            pages: 'Seiten',
            page: 'Seite',
            range: '{0}–{1} von {2}',
            first: 'Erste',
            previous: 'Zurück',
            next: 'Weiter',
            last: 'Letzte',
            openFailed: 'Der Reader konnte nicht an dieser Seite geöffnet werden.',
            current: 'Zuletzt gelesen'
        }
    };

    function t(key) {
        var lang = (document.documentElement.lang || navigator.language || 'en').slice(0, 2).toLowerCase();
        var table = TEXT[lang] || TEXT.en;
        var text = table[key] || TEXT.en[key];
        var args = Array.prototype.slice.call(arguments, 1);
        return text.replace(/\{(\d+)\}/g, function (m, i) {
            return args[i];
        });
    }

    /* ---------- API ---------- */

    function api() {
        return window.ApiClient;
    }

    function authHeader(client) {
        var parts = [
            'Client="' + encodeURIComponent(client.appName ? client.appName() : 'Jellyfin Web') + '"',
            'Device="' + encodeURIComponent(client.deviceName ? client.deviceName() : 'Browser') + '"',
            'DeviceId="' + encodeURIComponent(client.deviceId ? client.deviceId() : '') + '"',
            'Version="' + encodeURIComponent(client.appVersion ? client.appVersion() : '') + '"',
            'Token="' + encodeURIComponent(client.accessToken()) + '"'
        ];
        return 'MediaBrowser ' + parts.join(', ');
    }

    function request(path, options) {
        var client = api();
        options = options || {};
        return fetch(client.getUrl(path), {
            method: options.method || 'GET',
            headers: { Authorization: authHeader(client), Accept: options.accept || 'application/json' },
            signal: options.signal
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('HTTP ' + response.status + ' for ' + path);
            }
            return response;
        });
    }

    function getJson(path, signal) {
        return request(path, { signal: signal }).then(function (r) {
            return r.json();
        });
    }

    function getPages(itemId, startIndex, signal) {
        return getJson('ComicThumbnails/' + itemId + '/Pages?startIndex=' + startIndex, signal);
    }

    /* ---------- Opening the reader ---------- */

    // The web client follows "Play" commands the server sends to its own session, and passes
    // StartPositionTicks to the comic reader. That is the only public way to start the reader at a page.
    function openAtPage(itemId, index) {
        var client = api();
        var deviceId = client.deviceId();
        var userId = client.getCurrentUserId();

        return getJson('Sessions?deviceId=' + encodeURIComponent(deviceId))
            .then(function (sessions) {
                var own = (sessions || []).filter(function (s) {
                    return s.DeviceId === deviceId && (!userId || s.UserId === userId);
                })[0];
                if (!own) {
                    throw new Error('No session for this device');
                }
                var query = 'playCommand=PlayNow&itemIds=' + encodeURIComponent(itemId)
                    + '&startPositionTicks=' + (index * TICKS_PER_PAGE);
                return request('Sessions/' + own.Id + '/Playing?' + query, { method: 'POST' });
            })
            .catch(function (err) {
                console.error('[ComicThumbnails]', err);
                if (window.Dashboard && window.Dashboard.alert) {
                    window.Dashboard.alert(t('openFailed'));
                }
            });
    }

    /* ---------- Thumbnail loading (lazy, limited parallelism) ---------- */

    function Loader(itemId) {
        this.itemId = itemId;
        this.queue = [];
        this.active = 0;
        this.urls = [];
        this.abort = new AbortController();
        var self = this;
        this.observer = 'IntersectionObserver' in window
            ? new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        self.observer.unobserve(entry.target);
                        self.enqueue(entry.target);
                    }
                });
            }, { rootMargin: '300px' })
            : null;
    }

    Loader.prototype.watch = function (img) {
        if (this.observer) {
            this.observer.observe(img);
        } else {
            this.enqueue(img);
        }
    };

    Loader.prototype.enqueue = function (img) {
        this.queue.push(img);
        this.pump();
    };

    Loader.prototype.pump = function () {
        var self = this;
        while (this.active < MAX_PARALLEL_LOADS && this.queue.length) {
            var img = this.queue.shift();
            if (!img.isConnected) {
                continue;
            }
            this.active++;
            this.load(img).then(function () {
                self.active--;
                self.pump();
            });
        }
    };

    Loader.prototype.load = function (img) {
        var self = this;
        var path = 'ComicThumbnails/' + this.itemId + '/Pages/' + img.getAttribute('data-index') + '/Thumbnail';
        return request(path, { signal: this.abort.signal, accept: 'image/*' })
            .then(function (r) {
                return r.blob();
            })
            .then(function (blob) {
                var url = URL.createObjectURL(blob);
                self.urls.push(url);
                img.src = url;
                img.classList.add('comicThumbnails-loaded');
            })
            .catch(function (err) {
                if (err && err.name === 'AbortError') {
                    return;
                }
                img.classList.add('comicThumbnails-failed');
            });
    };

    Loader.prototype.dispose = function () {
        this.abort.abort();
        if (this.observer) {
            this.observer.disconnect();
        }
        this.queue = [];
        this.urls.forEach(function (url) {
            URL.revokeObjectURL(url);
        });
        this.urls = [];
    };

    /* ---------- Rendering ---------- */

    function addStyles() {
        if (document.getElementById('comicThumbnailsStyles')) {
            return;
        }
        var style = document.createElement('style');
        style.id = 'comicThumbnailsStyles';
        style.textContent = [
            '.comicThumbnailsSection .comicThumbnails-header{display:flex;align-items:center;gap:.5em;cursor:pointer;user-select:none;}',
            '.comicThumbnailsSection .comicThumbnails-header .sectionTitle{margin-bottom:.5em;}',
            '.comicThumbnailsSection .comicThumbnails-toggle{display:inline-block;transition:transform .2s;opacity:.7;}',
            '.comicThumbnailsSection.comicThumbnails-collapsed .comicThumbnails-toggle{transform:rotate(-90deg);}',
            '.comicThumbnailsSection.comicThumbnails-collapsed .comicThumbnails-body{display:none;}',
            '.comicThumbnailsSection .comicThumbnails-count{opacity:.6;font-size:.9em;margin-bottom:.5em;}',
            '.comicThumbnailsSection .comicThumbnails-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(110px,1fr));gap:1em;}',
            '.comicThumbnailsSection .comicThumbnails-tile{all:unset;box-sizing:border-box;cursor:pointer;display:flex;flex-direction:column;align-items:center;gap:.35em;border-radius:.3em;padding:.25em;}',
            '.comicThumbnailsSection .comicThumbnails-tile:hover,.comicThumbnailsSection .comicThumbnails-tile:focus-visible{background:rgba(127,127,127,.18);}',
            '.comicThumbnailsSection .comicThumbnails-tile:focus-visible{outline:2px solid currentColor;}',
            '.comicThumbnailsSection .comicThumbnails-frame{width:100%;aspect-ratio:2/3;border-radius:.2em;overflow:hidden;background:rgba(127,127,127,.15);display:flex;align-items:center;justify-content:center;}',
            '.comicThumbnailsSection .comicThumbnails-frame img{width:100%;height:100%;object-fit:contain;opacity:0;transition:opacity .25s;}',
            '.comicThumbnailsSection .comicThumbnails-frame img.comicThumbnails-loaded{opacity:1;}',
            '.comicThumbnailsSection .comicThumbnails-current .comicThumbnails-frame{box-shadow:0 0 0 3px #00a4dc;}',
            '.comicThumbnailsSection .comicThumbnails-label{font-size:.85em;opacity:.8;}',
            '.comicThumbnailsSection .comicThumbnails-pager{display:flex;align-items:center;justify-content:center;gap:.5em;margin:1em 0 .5em;flex-wrap:wrap;}',
            '.comicThumbnailsSection .comicThumbnails-pager button{min-width:2.5em;margin:0;padding:.4em .8em;}',
            '.comicThumbnailsSection .comicThumbnails-pager button[disabled]{opacity:.35;cursor:default;}',
            '.comicThumbnailsSection .comicThumbnails-range{min-width:9em;text-align:center;opacity:.8;}'
        ].join('\n');
        document.head.appendChild(style);
    }

    function button(label, title) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'raised emby-button';
        b.textContent = label;
        b.title = title;
        b.setAttribute('aria-label', title);
        return b;
    }

    function Section(item, first) {
        this.item = item;
        this.total = first.TotalCount;
        this.pageSize = first.Limit || 24;
        this.loader = null;
        this.generation = 0;
        // Reader progress is stored as page index * 10000 ticks.
        var ticks = (item.UserData && item.UserData.PlaybackPositionTicks) || 0;
        this.currentPage = ticks > 0 ? Math.floor(ticks / TICKS_PER_PAGE) : -1;

        var el = this.el = document.createElement('div');
        el.className = 'verticalSection detailVerticalSection ' + SECTION_CLASS;
        el.setAttribute('data-item-id', item.Id);
        if (first.StartCollapsed) {
            el.classList.add('comicThumbnails-collapsed');
        }

        var header = document.createElement('div');
        header.className = 'comicThumbnails-header padded-right';
        header.setAttribute('role', 'button');
        header.tabIndex = 0;
        var title = document.createElement('h2');
        title.className = 'sectionTitle sectionTitle-cards';
        title.innerHTML = '<span class="comicThumbnails-toggle">▾</span> ';
        title.appendChild(document.createTextNode(t('pages')));
        var count = document.createElement('span');
        count.className = 'comicThumbnails-count';
        count.textContent = '(' + this.total + ')';
        header.appendChild(title);
        header.appendChild(count);
        var self = this;
        var toggle = function () {
            el.classList.toggle('comicThumbnails-collapsed');
            header.setAttribute('aria-expanded', String(!el.classList.contains('comicThumbnails-collapsed')));
        };
        header.addEventListener('click', toggle);
        header.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                toggle();
            }
        });
        header.setAttribute('aria-expanded', String(!first.StartCollapsed));

        var body = document.createElement('div');
        body.className = 'comicThumbnails-body';
        this.grid = document.createElement('div');
        this.grid.className = 'comicThumbnails-grid';
        this.pager = document.createElement('div');
        this.pager.className = 'comicThumbnails-pager';
        body.appendChild(this.grid);
        body.appendChild(this.pager);

        el.appendChild(header);
        el.appendChild(body);

        this.grid.addEventListener('click', function (e) {
            var tile = e.target.closest('.comicThumbnails-tile');
            if (tile) {
                openAtPage(self.item.Id, parseInt(tile.getAttribute('data-index'), 10));
            }
        });

        // Start on the pager page that contains the last read page.
        var startIndex = this.currentPage > 0 ? Math.floor(this.currentPage / this.pageSize) * this.pageSize : 0;
        if (startIndex === first.StartIndex) {
            this.show(first);
        } else {
            this.goTo(startIndex);
        }
    }

    Section.prototype.goTo = function (startIndex) {
        var self = this;
        var generation = ++this.generation;
        getPages(this.item.Id, startIndex).then(function (result) {
            if (generation === self.generation && self.el.isConnected) {
                self.show(result);
            }
        }).catch(function (err) {
            console.error('[ComicThumbnails]', err);
        });
    };

    Section.prototype.show = function (result) {
        var self = this;
        if (this.loader) {
            this.loader.dispose();
        }
        this.loader = new Loader(this.item.Id);
        this.total = result.TotalCount;
        this.pageSize = result.Limit || this.pageSize;

        this.grid.textContent = '';
        result.Pages.forEach(function (page) {
            var tile = document.createElement('button');
            tile.type = 'button';
            tile.className = 'comicThumbnails-tile';
            tile.setAttribute('data-index', page.Index);
            var label = t('page') + ' ' + (page.Index + 1);
            tile.title = page.Index === self.currentPage ? label + ' · ' + t('current') : label;
            if (page.Index === self.currentPage) {
                tile.classList.add('comicThumbnails-current');
            }

            var frame = document.createElement('div');
            frame.className = 'comicThumbnails-frame';
            var img = document.createElement('img');
            img.alt = label;
            img.setAttribute('data-index', page.Index);
            img.decoding = 'async';
            frame.appendChild(img);

            var caption = document.createElement('span');
            caption.className = 'comicThumbnails-label';
            caption.textContent = String(page.Index + 1);

            tile.appendChild(frame);
            tile.appendChild(caption);
            self.grid.appendChild(tile);
            self.loader.watch(img);
        });

        this.renderPager(result.StartIndex, result.Pages.length);
    };

    Section.prototype.renderPager = function (start, shown) {
        var self = this;
        var size = this.pageSize;
        var lastStart = Math.max(0, Math.floor((this.total - 1) / size) * size);
        this.pager.textContent = '';
        if (this.total <= size) {
            return;
        }

        var nav = function (label, title, target, disabled) {
            var b = button(label, title);
            b.disabled = disabled;
            b.addEventListener('click', function () {
                self.goTo(target);
            });
            self.pager.appendChild(b);
        };

        nav('«', t('first'), 0, start <= 0);
        nav('‹', t('previous'), Math.max(0, start - size), start <= 0);
        var range = document.createElement('span');
        range.className = 'comicThumbnails-range';
        range.textContent = t('range', start + 1, start + shown, this.total);
        this.pager.appendChild(range);
        nav('›', t('next'), start + size, start + size >= this.total);
        nav('»', t('last'), lastStart, start >= lastStart);
    };

    Section.prototype.dispose = function () {
        this.generation++;
        if (this.loader) {
            this.loader.dispose();
        }
        this.el.remove();
    };

    /* ---------- Detail page detection ---------- */

    var section = null;
    var pending = null;
    // Item that was checked and has no thumbnails (not a comic), so it is not asked for again.
    var skippedId = null;

    function itemIdFromLocation() {
        var hash = window.location.hash || '';
        if (!/^#!?\/details/i.test(hash)) {
            return null;
        }
        var q = hash.indexOf('?');
        if (q < 0) {
            return null;
        }
        var params = new URLSearchParams(hash.slice(q + 1));
        var serverId = params.get('serverId');
        var client = api();
        if (serverId && client && client.serverId && client.serverId() && serverId !== client.serverId()) {
            return null;
        }
        return params.get('id');
    }

    function visibleDetailPage() {
        var pages = document.querySelectorAll('.itemDetailPage');
        for (var i = 0; i < pages.length; i++) {
            if (!pages[i].classList.contains('hide')) {
                return pages[i];
            }
        }
        return null;
    }

    function anchorIn(page) {
        // "Cast & Crew" (castCollapsible) holds the people; the section goes right below it.
        return page.querySelector('#castCollapsible')
            || page.querySelector('.detailPageContent')
            || null;
    }

    function removeSection() {
        if (section) {
            section.dispose();
            section = null;
        }
    }

    function update() {
        var client = api();
        var id = itemIdFromLocation();
        var page = visibleDetailPage();

        if (!id) {
            removeSection();
            skippedId = null;
            return;
        }

        if (!client || !client.accessToken || !client.accessToken() || !page || id === skippedId) {
            return;
        }

        if (section && section.item.Id === id && section.el.isConnected && page.contains(section.el)) {
            // Jellyfin re-renders the people when the page refreshes; keep the section right below them.
            var cast = page.querySelector('#castCollapsible');
            if (cast && cast.nextElementSibling !== section.el) {
                cast.insertAdjacentElement('afterend', section.el);
            }
            return;
        }

        if (pending === id) {
            return;
        }
        removeSection();
        pending = id;

        client.getItem(client.getCurrentUserId(), id)
            .then(function (item) {
                if (!item || item.Type !== 'Book') {
                    return null;
                }
                return getPages(id, 0).then(function (first) {
                    return { item: item, first: first };
                });
            })
            .then(function (data) {
                if (!data || !data.first || !data.first.TotalCount) {
                    skippedId = id;
                    return;
                }
                var target = visibleDetailPage();
                if (itemIdFromLocation() !== id || !target) {
                    return;
                }
                var anchor = anchorIn(target);
                if (!anchor) {
                    return;
                }
                addStyles();
                removeSection();
                section = new Section(data.item, data.first);
                if (anchor.id === 'castCollapsible') {
                    anchor.insertAdjacentElement('afterend', section.el);
                } else {
                    anchor.appendChild(section.el);
                }
            })
            .catch(function (err) {
                skippedId = id;
                console.warn('[ComicThumbnails]', err);
            })
            .then(function () {
                if (pending === id) {
                    pending = null;
                }
            });
    }

    var scheduled = false;
    function schedule() {
        if (scheduled) {
            return;
        }
        scheduled = true;
        setTimeout(function () {
            scheduled = false;
            update();
        }, 150);
    }

    document.addEventListener('viewshow', schedule, true);
    window.addEventListener('hashchange', schedule);
    new MutationObserver(schedule).observe(document.body || document.documentElement, { childList: true, subtree: true });
    schedule();
})();
