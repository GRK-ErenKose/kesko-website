/* ==========================================================================
   Kesko — arayuz etkilesimleri
   Bagimlilik yok, framework yok. Tek IIFE, defer ile yuklenir.
   Icerik: mobil menu, gramaj secici, SSS akordiyonu, scroll ile beliren
   bloklar, siparis hatti hesaplayicisi, tedarik formu dogrulamasi.
   ========================================================================== */

(function () {
  "use strict";
  var nav = document.getElementById('nav'), burger = document.getElementById('burger');
  addEventListener('scroll', function(){ nav.classList.toggle('on', scrollY > 24); }, {passive:true});
  nav.classList.toggle('on', scrollY > 24);
  burger.addEventListener('click', function(){
    var o = nav.classList.toggle('open');
    burger.setAttribute('aria-expanded', o ? 'true' : 'false');
  });
  document.getElementById('menu').addEventListener('click', function(e){
    if(e.target.closest('a')){ nav.classList.remove('open'); burger.setAttribute('aria-expanded','false'); }
  });


  var caps = ["Bi'Dilim · Kakaolu Kek · 100 g / 40 g", "Bi'Dilim · Mozaik Kek · 100 g / 40 g", "Bi'Dilim · Sade Kek · çok yakında", "Bi'Dilim · Limonlu Kek · çok yakında"];
  var cap = document.getElementById('cap'), ci = 0;
  if(!matchMedia('(prefers-reduced-motion:reduce)').matches){
    setInterval(function(){ ci = (ci + 1) % caps.length; cap.textContent = caps[ci]; }, 5000);
  }

  document.querySelectorAll('.gram').forEach(function(g){
    var prod = g.closest('.prod'), img = prod.querySelector('.media img'), spec = prod.querySelector('[data-spec]');
    g.addEventListener('click', function(e){
      var b = e.target.closest('button'); if(!b) return;
      g.querySelectorAll('button').forEach(function(x){ x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); });
      var small = b.dataset.g === '40';
      if(img) img.classList.toggle('g40', small);
      if(spec) spec.textContent = small ? 'Küçük dilim · 40 g' : 'Tek dilim · 100 g';
    });
  });

  document.getElementById('faq').addEventListener('click', function(e){
    var b = e.target.closest('button'); if(!b) return;
    var open = b.getAttribute('aria-expanded') === 'true';
    this.querySelectorAll('button').forEach(function(x){
      x.setAttribute('aria-expanded','false');
      x.nextElementSibling.style.maxHeight = '0px';
    });
    if(!open){ b.setAttribute('aria-expanded','true'); var a = b.nextElementSibling; a.style.maxHeight = a.scrollHeight + 'px'; }
  });

  var io = new IntersectionObserver(function(es){
    es.forEach(function(e){ if(e.isIntersecting){ e.target.classList.add('in'); io.unobserve(e.target); } });
  }, {threshold:.1, rootMargin:'0px 0px -6% 0px'});
  document.querySelectorAll('.rv').forEach(function(n){ io.observe(n); });

  // ---- siparis hatti ----
  // Varsayilanlar. Sunucu calisiyorsa /api/site-ayar bunlari ezer; statik
  // olarak acildiginda (dosya olarak, sunucusuz) asagidaki degerler gecerli.
  var FREE = 200, WA = '900000000000', MAIL = 'merhaba@kesko.com.tr';
  var oProd = document.getElementById('o-prod'), oGram = document.getElementById('o-gram');
  var oQty = document.getElementById('o-qty'), oCity = document.getElementById('o-city');
  var oZone = document.getElementById('o-zone'), sShip = document.getElementById('s-ship');
  var sProd = document.getElementById('s-prod'), sGram = document.getElementById('s-gram');
  var sQty = document.getElementById('s-qty'), sBar = document.getElementById('s-bar');
  var sNote = document.getElementById('s-note'), oWa = document.getElementById('o-wa');
  var oSend = document.getElementById('o-send');
  var oErr = document.getElementById('o-err'), oOk = document.getElementById('o-ok');

  function pick(group){
    var b = group.querySelector('[aria-pressed="true"]');
    return b ? b.dataset.v : '';
  }
  function qty(){
    var n = parseInt(oQty.value, 10);
    return (isNaN(n) || n < 1) ? 1 : n;
  }
  function msg(){
    return 'Kesko siparis talebi\n\nUrun: ' + pick(oProd) +
           '\nGramaj: ' + pick(oGram) +
           '\nAdet: ' + qty() +
           '\nBolge: ' + (pick(oZone) === 'ic' ? 'Ankara ici' : 'Ankara disi') +
           '\nSehir/ilce: ' + ((oCity.value || '').trim() || '-') +
           '\nKargo: ' + (qty() >= FREE ? 'Bedava (200 adet uzeri)' : 'Ucretli') +
           '\nTeslimat: ' + (pick(oZone) === 'ic' ? 'Ankara ici dagitim' : '2 is gunu, kargo');
  }
  function sync(){
    var n = qty();
    sProd.textContent = pick(oProd);
    sGram.textContent = pick(oGram);
    sQty.textContent = n.toLocaleString('tr-TR');
    var ic = pick(oZone) === 'ic';
    sShip.textContent = ic ? 'Ankara içi dağıtım' : '2 iş günü, kargo';
    oCity.placeholder = ic ? 'İlçe' : 'Şehir';
    var pct = Math.min(n / FREE, 1) * 100;
    sBar.querySelector('b').style.width = pct + '%';
    sBar.classList.toggle('free', n >= FREE);
    sNote.classList.toggle('free', n >= FREE);
    sNote.innerHTML = n >= FREE
      ? 'Kargo bedava. <strong>Türkiye\'nin her yerine 2 günde</strong> teslim.'
      : 'Kargo bedava için <strong>' + (FREE - n).toLocaleString('tr-TR') + ' adet</strong> daha.';
    oWa.href = 'https://wa.me/' + WA + '?text=' + encodeURIComponent(msg());
  }
  [oProd, oGram, oZone].forEach(function(g){
    g.addEventListener('click', function(e){
      var b = e.target.closest('button'); if(!b) return;
      g.querySelectorAll('button').forEach(function(x){ x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); });
      sync();
    });
  });
  oQty.addEventListener('input', sync);
  oCity.addEventListener('input', sync);
  document.getElementById('o-minus').addEventListener('click', function(){ oQty.value = Math.max(1, qty() - 10); sync(); });
  document.getElementById('o-plus').addEventListener('click', function(){ oQty.value = qty() + 10; sync(); });
  document.querySelectorAll('.quick button').forEach(function(b){
    b.addEventListener('click', function(){ oQty.value = b.dataset.q; sync(); });
  });
  sync();

  // ---- sunucuya gonderim ----------------------------------------------
  // Istemci dogrulamasi yalnizca kullanici rahatligi icindir; gercek
  // dogrulama sunucuda (ASP.NET Core) tekrar yapilir.

  function v(id){ var e = document.getElementById(id); return e ? (e.value || '').trim() : ''; }
  function epostaGecerli(e){ return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(e); }
  function telefonGecerli(t){ return (t.match(/\d/g) || []).length >= 10; }

  // ---- telefon maskesi ------------------------------------------------
  // Turkiye numaralari her zaman 11 hane: bastaki 0 + 10 rakam.
  // Bicim: 0535 011 80 21  (4 + 3 + 2 + 2)
  // Kullanici bastaki sifiri yazmazsa maske kendisi ekler. Ulke kodu
  // (+90 / 0090 / 90) yazilirsa yutulur. Alan bos kalirsa bos gorunur.

  /**
   * Girdiyi kanonik 10 rakama indirger ve bastan kac rakam atildigini
   * da bildirir. "atilan" imlec hesabi icin sart: maskenin bastaki "0"i
   * kullanicinin yazdigi bir rakam degil, biz ekliyoruz.
   */
  function telAyir(ham){
    var d = (ham || '').replace(/\D/g, '');
    var bas = 0;
    if(d.indexOf('0090') === 0) bas = 4;
    else if(d.indexOf('90') === 0 && d.length > 10) bas = 2;
    if(d.charAt(bas) === '0') bas += 1;
    return { rakam: d.slice(bas, bas + 10), atilan: bas };
  }

  /** Her turlu girdiyi bastaki sifir ve ulke kodu olmadan 10 rakama indirger. */
  function telRakam(ham){
    return telAyir(ham).rakam;
  }

  function telBicim(ham){
    var d = telRakam(ham);
    if(!d) return '';
    var s = '0' + d.slice(0, 3);                       // 0535
    if(d.length > 3) s += ' ' + d.slice(3, 6);         // 011
    if(d.length > 6) s += ' ' + d.slice(6, 8);         // 80
    if(d.length > 8) s += ' ' + d.slice(8, 10);        // 21
    return s;
  }

  function telMaskele(alan){
    if(!alan) return;

    function uygula(){
      var eski = alan.value;
      // Yurt disi numarasi: '+' ile baslayip ulke kodu 90 degilse
      // maskeye zorlamiyoruz, kullanici ne yazdiysa oyle kalir.
      if(eski.charAt(0) === '+' && eski.replace(/\D/g, '').indexOf('90') !== 0) return;

      var yeni = telBicim(eski);
      if(yeni === eski) return;

      // Imleci, kullanicinin yazdigi ayni rakamin arkasina geri koyuyoruz.
      // Sayim "kullanici rakami" uzerinden yapiliyor: onekten atilan
      // rakamlar (bastaki 0, ulke kodu) ve maskenin bastaki "0"i
      // sayilmaz. Aksi halde imlec 0 ile ilk rakamin arasinda kalir ve
      // sonraki tuslar araya yazilir.
      var imlec = alan.selectionStart;
      var tumu = telAyir(eski);
      var onekRakam = (eski.slice(0, imlec).match(/\d/g) || []).length;
      var hedef = Math.max(0, Math.min(onekRakam - tumu.atilan, tumu.rakam.length));

      alan.value = yeni;

      var konum;
      if(hedef === 0){
        konum = yeni.length ? 1 : 0;        // maskenin "0"inin hemen arkasi
      } else {
        konum = yeni.length;
        var sayac = 0;
        for(var i = 0; i < yeni.length; i++){
          if(!/\d/.test(yeni.charAt(i))) continue;
          sayac++;                          // sayac 1 => maskenin "0"i
          if(sayac - 1 === hedef){ konum = i + 1; break; }
        }
      }

      // Odakta degilken (otomatik doldurma, yapistirma sonrasi) imlece
      // dokunmanin anlami yok.
      if(document.activeElement === alan){
        try { alan.setSelectionRange(konum, konum); } catch(e){ /* type=tel desteklemiyor */ }
      }
    }

    alan.addEventListener('input', uygula);

    // Ayirici uzerinde geri silme: maskenin parantezi yerine bir onceki
    // rakami siler, yoksa tus calismiyormus gibi gorunur.
    alan.addEventListener('keydown', function(e){
      if(e.key !== 'Backspace') return;
      var k = alan.selectionStart;
      if(k === null || k !== alan.selectionEnd || k === 0) return;
      if(/\d/.test(alan.value.charAt(k - 1))) return;

      var i = k - 1;
      while(i >= 0 && !/\d/.test(alan.value.charAt(i))) i--;
      if(i < 0) return;

      e.preventDefault();
      alan.value = alan.value.slice(0, i) + alan.value.slice(i + 1);
      try { alan.setSelectionRange(i, i); } catch(err){ }
      uygula();
    });

    // Yapistirilan ya da tarayicinin otomatik doldurdugu deger de bicimlenir.
    alan.addEventListener('change', uygula);
    if(alan.value) uygula();
  }

  telMaskele(document.getElementById('o-tel'));
  telMaskele(document.getElementById('f-tl'));

  /* Formu gonderir, dugmeyi kilitler, sonucu sayfa icinde bildirir.
     istek: {url, govde, dugme, hataEl, okEl, basariMetni} */
  function gonder(istek){
    var dugme = istek.dugme, eski = dugme.textContent;
    dugme.disabled = true;
    dugme.textContent = 'Gönderiliyor…';
    istek.hataEl.classList.remove('show');
    istek.okEl.classList.remove('show');

    fetch(istek.url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(istek.govde)
    })
      .then(function(y){ return y.json().then(function(j){ return { durum: y.ok, veri: j }; }); })
      .then(function(s){
        if(s.durum && s.veri.ok){
          istek.okEl.textContent = s.veri.referans
            ? s.veri.mesaj + ' Referans numaranız: ' + s.veri.referans
            : s.veri.mesaj;
          istek.okEl.classList.add('show');
          if(istek.temizle) istek.temizle();
        } else {
          istek.hataEl.textContent = (s.veri && s.veri.mesaj) || 'Gönderilemedi. Lütfen tekrar deneyin.';
          istek.hataEl.classList.add('show');
        }
      })
      .catch(function(){
        // Sunucuya ulasilamadi: e-posta ile gonderme secenegini sun.
        istek.hataEl.innerHTML = 'Bağlantı kurulamadı. ' +
          '<a href="mailto:' + MAIL + '" style="text-decoration:underline">' + MAIL +
          '</a> adresine yazabilir ya da WhatsApp\'tan ulaşabilirsiniz.';
        istek.hataEl.classList.add('show');
      })
      .then(function(){
        dugme.disabled = false;
        dugme.textContent = eski;
      });
  }

  // --- siparis hatti gonderimi ---
  ['o-name','o-mailadr','o-tel'].forEach(function(id){
    var e = document.getElementById(id);
    if(e) e.addEventListener('input', function(){ oErr.classList.remove('show'); });
  });

  oSend.addEventListener('click', function(){
    var ad = v('o-name'), ml = v('o-mailadr'), tl = v('o-tel');
    if(!ad || (!epostaGecerli(ml) && !telefonGecerli(tl))){
      oErr.textContent = 'Ad soyad ve geçerli bir e-posta ya da telefon girin.';
      oErr.classList.add('show');
      oOk.classList.remove('show');
      return;
    }
    gonder({
      url: '/api/siparis',
      dugme: oSend, hataEl: oErr, okEl: oOk,
      govde: {
        urun: pick(oProd), gramaj: pick(oGram), adet: qty(),
        bolge: pick(oZone), sehir: v('o-city'),
        yetkili: ad, firma: v('o-co'), eposta: ml, telefon: tl,
        mesaj: v('o-note'), website: v('o-hp')
      },
      temizle: function(){
        ['o-name','o-co','o-mailadr','o-tel','o-note'].forEach(function(id){
          var e = document.getElementById(id); if(e) e.value = '';
        });
      }
    });
  });

  // --- toplu tedarik gonderimi ---
  var send = document.getElementById('send'), err = document.getElementById('err'), ok = document.getElementById('ok');
  ['f-co','f-nm','f-ml'].forEach(function(id){
    document.getElementById(id).addEventListener('input', function(){ err.classList.remove('show'); });
  });

  send.addEventListener('click', function(){
    var co = v('f-co'), nm = v('f-nm'), ml = v('f-ml');
    if(!co || !nm || !epostaGecerli(ml)){
      err.textContent = 'Firma adı, yetkili kişi ve geçerli bir e-posta girin.';
      err.classList.add('show');
      ok.classList.remove('show');
      return;
    }
    gonder({
      url: '/api/teklif',
      dugme: send, hataEl: err, okEl: ok,
      govde: {
        firma: co, yetkili: nm, eposta: ml, telefon: v('f-tl'),
        sehir: v('f-ct'), hat: v('f-ln'), gramaj: v('f-gr'),
        mesaj: v('f-ms'), website: v('f-hp')
      },
      temizle: function(){
        ['f-co','f-nm','f-ml','f-tl','f-ct','f-ms'].forEach(function(id){
          var e = document.getElementById(id); if(e) e.value = '';
        });
      }
    });
  });

  // ---- panelden yonetilen site sabitleri ------------------------------
  // Uc yoksa (statik barindirma) sessizce varsayilanlarla devam edilir.
  fetch('/api/site-ayar', { headers: { 'Accept': 'application/json' } })
    .then(function(y){ return y.ok ? y.json() : null; })
    .then(function(a){
      if(!a) return;
      if(a.kargoBedavaEsik > 0) FREE = a.kargoBedavaEsik;
      if(a.whatsapp) WA = a.whatsapp;
      if(a.eposta) MAIL = a.eposta;

      document.querySelectorAll('[data-site-eposta]').forEach(function(e){
        e.textContent = a.eposta;
      });
      document.querySelectorAll('[data-site-telefon]').forEach(function(e){
        e.textContent = a.telefon;
      });
      var waSabit = document.querySelector('a.wa');
      if(waSabit && a.whatsapp){
        waSabit.href = 'https://wa.me/' + a.whatsapp +
          '?text=' + encodeURIComponent('Merhaba Kesko, ürünleriniz hakkında bilgi almak istiyorum.');
      }

      if(a.formlarAktif === false){
        [oSend, send].forEach(function(b){ if(b){ b.disabled = true; b.style.opacity = '.55'; } });
        var mesaj = a.bakimMesaji || 'Form geçici olarak kapalı. Lütfen telefonla ulaşın.';
        [oErr, err].forEach(function(e){ if(e){ e.textContent = mesaj; e.classList.add('show'); } });
      }

      sync();
    })
    .catch(function(){ /* statik barindirma: varsayilanlar gecerli */ });
})();
