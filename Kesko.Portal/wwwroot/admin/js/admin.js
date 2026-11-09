/* ==========================================================================
   Kesko — yonetim paneli etkilesimleri
   Bagimlilik yok. Her blok kendi kokunu arar; o kok sayfada yoksa sessizce
   atlanir, boylece tek dosya tum sayfalarda yuklenebilir.
   ========================================================================== */
(function () {
  "use strict";

  var d = document, kok = d.documentElement;

  /* ---- tema -------------------------------------------------------------
     Secim localStorage'da tutulur; ilk boyama _Layout icindeki kucuk
     betikte yapildigi icin burada yalnizca degistirme islemi var. */
  function temaKur() {
    var dug = d.getElementById('tema-dug');
    if (!dug) return;
    dug.addEventListener('click', function () {
      var yeni = kok.dataset.tema === 'koyu' ? 'acik' : 'koyu';
      kok.dataset.tema = yeni;
      try { localStorage.setItem('kesko.tema', yeni); } catch (e) { /* gizli sekme */ }
      dug.setAttribute('aria-label', yeni === 'koyu' ? 'Açık temaya geç' : 'Koyu temaya geç');
    });
  }

  /* ---- kenar cubugu (mobil) -------------------------------------------- */
  function yanKur() {
    var dug = d.getElementById('menu-dug'), perde = d.getElementById('perde');
    if (!dug) return;
    function kapa() { d.body.classList.remove('yan-acik'); dug.setAttribute('aria-expanded', 'false'); }
    dug.addEventListener('click', function () {
      var acik = d.body.classList.toggle('yan-acik');
      dug.setAttribute('aria-expanded', acik ? 'true' : 'false');
    });
    if (perde) perde.addEventListener('click', kapa);
    d.addEventListener('keydown', function (e) { if (e.key === 'Escape') kapa(); });
  }

  /* ---- acilir menuler -------------------------------------------------- */
  function acilirKur() {
    d.addEventListener('click', function (e) {
      var tetik = e.target.closest('[data-acilir]');
      d.querySelectorAll('.acilir.acik').forEach(function (a) {
        if (!tetik || a !== tetik.closest('.acilir')) {
          a.classList.remove('acik');
          var t = a.querySelector('[data-acilir]');
          if (t) t.setAttribute('aria-expanded', 'false');
        }
      });
      if (tetik) {
        var sar = tetik.closest('.acilir');
        var acik = sar.classList.toggle('acik');
        tetik.setAttribute('aria-expanded', acik ? 'true' : 'false');
      }
    });
    d.addEventListener('keydown', function (e) {
      if (e.key !== 'Escape') return;
      d.querySelectorAll('.acilir.acik').forEach(function (a) { a.classList.remove('acik'); });
    });
  }

  /* ---- bildirim (toast) ------------------------------------------------- */
  var SIMGE = {
    ok: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg>',
    hata: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M12 8v5M12 16.5v.5"/><circle cx="12" cy="12" r="9"/></svg>',
    bilgi: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M12 11v5M12 7.5v.5"/><circle cx="12" cy="12" r="9"/></svg>'
  };

  function bildir(metin, tur) {
    var yuva = d.getElementById('bildirim-yuva');
    if (!yuva) return;
    tur = tur || 'bilgi';
    var el = d.createElement('div');
    el.className = 'bildirim ' + tur;
    el.setAttribute('role', tur === 'hata' ? 'alert' : 'status');
    el.innerHTML = (SIMGE[tur] || SIMGE.bilgi) +
      '<span></span><button class="kapat" aria-label="Kapat">&times;</button>';
    el.querySelector('span').textContent = metin;
    yuva.appendChild(el);

    var zaman = setTimeout(kapat, tur === 'hata' ? 7000 : 4200);
    function kapat() {
      clearTimeout(zaman);
      el.classList.add('gider');
      setTimeout(function () { el.remove(); }, 220);
    }
    el.querySelector('.kapat').addEventListener('click', kapat);
  }
  window.keskoBildir = bildir;

  // Sunucudan gelen tek seferlik mesajlar (_Layout icinde data- olarak basilir)
  function sunucuMesaji() {
    var kutu = d.getElementById('sunucu-mesaj');
    if (!kutu) return;
    if (kutu.dataset.ok) bildir(kutu.dataset.ok, 'ok');
    if (kutu.dataset.hata) bildir(kutu.dataset.hata, 'hata');
    if (kutu.dataset.bilgi) bildir(kutu.dataset.bilgi, 'bilgi');
  }

  /* ---- uyari kapatma ---------------------------------------------------- */
  function uyariKur() {
    d.addEventListener('click', function (e) {
      var k = e.target.closest('.uyari .kapat');
      if (k) k.closest('.uyari').remove();
    });
  }

  /* ---- filtre: degisince gonder, temizle ------------------------------- */
  function filtreKur() {
    var form = d.getElementById('filtre-form');
    if (!form) return;

    form.querySelectorAll('[data-oto]').forEach(function (el) {
      el.addEventListener('change', function () { form.requestSubmit(); });
    });

    // Serbest metin alaninda yazmayi bitirmeyi bekle.
    var ara = form.querySelector('[data-gecikmeli]');
    if (ara) {
      var zaman;
      ara.addEventListener('input', function () {
        clearTimeout(zaman);
        zaman = setTimeout(function () { form.requestSubmit(); }, 450);
      });
    }

    var gelismis = d.getElementById('gelismis');
    var gelismisDug = d.getElementById('gelismis-dug');
    if (gelismis && gelismisDug) {
      var acikBasta = gelismisDug.getAttribute('aria-expanded') === 'true';
      gelismis.hidden = !acikBasta;
      gelismisDug.addEventListener('click', function () {
        var acik = gelismis.hidden;
        gelismis.hidden = !acik;
        gelismisDug.setAttribute('aria-expanded', acik ? 'true' : 'false');
      });
    }
  }

  /* ---- tablo: toplu secim ---------------------------------------------- */
  function topluKur() {
    var tablo = d.getElementById('talep-tablo');
    var serit = d.getElementById('toplu-serit');
    if (!tablo || !serit) return;

    var hepsi = tablo.querySelector('[data-hepsi]');
    function kutular() { return Array.prototype.slice.call(tablo.querySelectorAll('[data-sec]')); }

    function tazele() {
      var secili = kutular().filter(function (k) { return k.checked; });
      serit.classList.toggle('acik', secili.length > 0);
      var say = serit.querySelector('[data-sayi]');
      if (say) say.textContent = secili.length;
      if (hepsi) {
        hepsi.checked = secili.length > 0 && secili.length === kutular().length;
        hepsi.indeterminate = secili.length > 0 && secili.length < kutular().length;
      }
      serit.querySelectorAll('[name="secili"]').forEach(function (n) { n.remove(); });
      secili.forEach(function (k) {
        var g = d.createElement('input');
        g.type = 'hidden'; g.name = 'secili'; g.value = k.value;
        serit.querySelector('form').appendChild(g);
      });
    }

    if (hepsi) hepsi.addEventListener('change', function () {
      kutular().forEach(function (k) { k.checked = hepsi.checked; });
      tazele();
    });
    tablo.addEventListener('change', function (e) {
      if (e.target.matches('[data-sec]')) tazele();
    });

    var vazgec = serit.querySelector('[data-vazgec]');
    if (vazgec) vazgec.addEventListener('click', function () {
      kutular().forEach(function (k) { k.checked = false; });
      tazele();
    });
  }

  /* ---- grafik ipucu -----------------------------------------------------
     SVG grafiklerde her veri noktasi <g data-ipucu="..."> olarak basilir;
     fare uzerine gelince konumlandirilmis bir kutu gosterilir. */
  function ipucuKur() {
    var kutu = null;
    function kutuAl() {
      if (kutu) return kutu;
      kutu = d.createElement('div');
      kutu.style.cssText =
        'position:fixed;z-index:300;pointer-events:none;opacity:0;transition:opacity .12s;' +
        'background:#1C1A4D;color:#fff;font:600 12px/1.45 Inter,system-ui,sans-serif;' +
        'padding:7px 10px;border-radius:8px;box-shadow:0 8px 26px -8px rgba(0,0,0,.5);white-space:pre';
      d.body.appendChild(kutu);
      return kutu;
    }

    d.addEventListener('mouseover', function (e) {
      var hedef = e.target.closest('[data-ipucu]');
      if (!hedef) return;
      var k = kutuAl();
      k.textContent = hedef.getAttribute('data-ipucu');
      k.style.opacity = '1';
      yerlestir(e, k);
    });
    d.addEventListener('mousemove', function (e) {
      if (!kutu || kutu.style.opacity !== '1') return;
      yerlestir(e, kutu);
    });
    d.addEventListener('mouseout', function (e) {
      if (!e.target.closest('[data-ipucu]') || !kutu) return;
      kutu.style.opacity = '0';
    });

    function yerlestir(e, k) {
      var g = k.offsetWidth, y = k.offsetHeight;
      var x = Math.min(Math.max(8, e.clientX - g / 2), innerWidth - g - 8);
      var ust = e.clientY - y - 12;
      k.style.left = x + 'px';
      k.style.top = (ust < 8 ? e.clientY + 16 : ust) + 'px';
    }
  }

  /* ---- klavye kisayollari ---------------------------------------------- */
  function kisayolKur() {
    d.addEventListener('keydown', function (e) {
      var yaziyor = /^(INPUT|TEXTAREA|SELECT)$/.test(d.activeElement && d.activeElement.tagName);
      if (e.key === '/' && !yaziyor) {
        var ara = d.getElementById('ust-arama') || d.querySelector('[data-gecikmeli]');
        if (ara) { e.preventDefault(); ara.focus(); ara.select(); }
      }
      if (e.key === 'Escape' && yaziyor) d.activeElement.blur();
    });
  }

  /* ---- onay gerektiren islemler ---------------------------------------- */
  function onayKur() {
    d.addEventListener('submit', function (e) {
      var form = e.target;
      var soru = form.getAttribute('data-onay');
      if (soru && !confirm(soru)) e.preventDefault();
    });
    d.addEventListener('click', function (e) {
      var dug = e.target.closest('[data-onay-dug]');
      if (dug && !confirm(dug.getAttribute('data-onay-dug'))) e.preventDefault();
    });
  }

  /* ---- panoya kopyala --------------------------------------------------- */
  function kopyaKur() {
    d.addEventListener('click', function (e) {
      var dug = e.target.closest('[data-kopyala]');
      if (!dug) return;
      var metin = dug.getAttribute('data-kopyala');
      if (navigator.clipboard) {
        navigator.clipboard.writeText(metin)
          .then(function () { bildir('Panoya kopyalandı.', 'ok'); })
          .catch(function () { bildir('Kopyalanamadı.', 'hata'); });
      } else {
        bildir('Tarayıcı panoya kopyalamayı desteklemiyor.', 'hata');
      }
    });
  }

  /* ---- gonderim sirasinda dugmeyi kilitle ------------------------------ */
  function kilitKur() {
    d.addEventListener('submit', function (e) {
      if (e.defaultPrevented) return;
      var dug = e.target.querySelector('[data-bekle]');
      if (!dug) return;
      // Submit degeri gitsin, sonra kilitle.
      setTimeout(function () {
        dug.disabled = true;
        var eski = dug.textContent;
        dug.dataset.eski = eski;
        dug.textContent = dug.getAttribute('data-bekle');
      }, 0);
    });
  }

  temaKur(); yanKur(); acilirKur(); sunucuMesaji(); uyariKur();
  filtreKur(); topluKur(); ipucuKur(); kisayolKur(); onayKur(); kopyaKur(); kilitKur();
})();
