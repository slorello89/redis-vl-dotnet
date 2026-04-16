document.addEventListener('DOMContentLoaded', () => {
  const body = document.body;
  const navToggle = document.querySelector('[data-nav-toggle]');

  navToggle?.addEventListener('click', () => {
    body.classList.toggle('nav-open');
  });

  highlightNavigation();
  buildPageToc();
});

function highlightNavigation() {
  const links = [...document.querySelectorAll('.nav-tree__link[data-nav-url]')];
  if (links.length === 0) {
    return;
  }

  const currentPath = normalizePath(window.location.pathname);
  const currentLink = links.find((link) => normalizePath(new URL(link.href, window.location.href).pathname) === currentPath);
  if (!currentLink) {
    return;
  }

  currentLink.classList.add('is-current-link');

  let item = currentLink.closest('.nav-tree__item');
  while (item) {
    item.classList.add('is-current-branch');
    item = item.parentElement?.closest('.nav-tree__item') ?? null;
  }
}

function buildPageToc() {
  const toc = document.getElementById('page-toc');
  const aside = document.querySelector('.sidebar-toc');
  const headings = [...document.querySelectorAll('.doc-content h2[id], .doc-content h3[id]')];

  if (!toc || !aside || headings.length === 0) {
    aside?.classList.add('is-empty');
    return;
  }

  const list = document.createElement('ol');

  headings.forEach((heading) => {
    const item = document.createElement('li');
    item.className = heading.tagName === 'H3' ? 'toc-item is-subitem' : 'toc-item';

    const link = document.createElement('a');
    link.href = `#${heading.id}`;
    link.textContent = heading.textContent ?? '';

    item.appendChild(link);
    list.appendChild(item);
  });

  toc.replaceChildren(list);
}

function normalizePath(pathname) {
  return pathname.replace(/index\.html$/, '').replace(/\/+$/, '') || '/';
}
