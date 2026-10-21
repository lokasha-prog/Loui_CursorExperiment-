/* Nursery website: English/Arabic text, centre name from the NMS server, and the enquiry form that sends to the centre's pipeline. */
(function () {
  'use strict'
  var cfg = window.SITE_CONFIG || {}
  var base = (cfg.apiBase || '') + '/api/v1/public'

  var T = {
    en: {
      skip: 'Skip to content', brand: 'Bright Futures',
      nav: { about: 'About', day: 'A day with us', parents: 'Parents', faq: 'Questions', enquire: 'Ask about a place' },
      hero: { kicker: 'Early years in the UAE', title: 'A happy, safe place where every child is known and loved', lead: 'Small groups, caring teachers and a calm, playful day. We keep you close to your child\'s day, in English and Arabic.', cta1: 'Book a visit', cta2: 'See a day with us' },
      about: { kicker: 'Why families choose us', title: 'Care you can see, learning you can feel', c1t: 'Known and loved', c1: 'Every child has a key person who knows their routines, likes and needs.', c2t: 'Safe by design', c2: 'Secure check-in and check-out, named pick-up people and the right number of staff in every room, all day.', c3t: 'English and Arabic', c3: 'Songs, stories and conversations in both languages so children grow confident in each.', c4t: 'Close to your child\'s day', c4: 'Photos, meals, naps and daily reports in our parent app, with messages to the team.' },
      day: { kicker: 'A day with us', title: 'A gentle rhythm of play, rest and discovery', s1t: 'Warm welcome', s1: 'Children are greeted by name and settle in with free play.', s2t: 'Circle time and stories', s2: 'Songs, languages and storytelling to start the day together.', s3t: 'Play and learning', s3: 'Indoor and outdoor activities for movement, creativity and early skills.', s4t: 'Healthy meals and rest', s4: 'Nutritious food that respects every family\'s needs, then a quiet rest.', s5t: 'Home with a daily report', s5: 'You hear how the day went, with photos and notes, before you arrive.' },
      parents: { kicker: 'For parents', title: 'Everything in one place', c1t: 'Parent app', c1: 'See the feed, daily reports and messages, and choose which alerts you get.', c2t: 'Invoices and forms', c2: 'View invoices, fill in forms and read newsletters without paper.', c3t: 'Your privacy', c3: 'Photos and details are shared only with your family. We follow UAE data-protection rules.', portal: 'Parent sign-in' },
      faq: { kicker: 'Questions', title: 'Things families often ask', q1: 'Can I visit before I decide?', a1: 'Yes. Send us the form below and we will arrange a tour at a time that suits you.', q2: 'What ages do you take?', a2: 'Tell us your child\'s date of birth in the form and we will explain which group is right.', q3: 'How do I know my child is safe?', a3: 'Children are checked in and out securely, only named people may collect them, and we keep staff cover in every room.', q4: 'What about fees?', a4: 'Fees depend on the programme and days. We will send a clear breakdown when you enquire.' },
      form: { kicker: 'Ask about a place', title: 'Tell us about your child', note: 'We will get back to you soon. Only our team sees what you send.', family: 'Your name', child: 'Child\'s name', phone: 'Phone', email: 'E-mail', dob: 'Child\'s date of birth', start: 'Wished start date', contact: 'How should we contact you?', byPhone: 'Phone', byEmail: 'E-mail', message: 'Anything you would like us to know', send: 'Send', sending: 'Sending...', needNames: 'Please tell us your name and your child\'s name.', needContact: 'Please give a phone number or an e-mail so we can reach you.', ok: 'Thank you! We have your message and will be in touch soon.', fail: 'Sorry, we could not send this just now. Please try again in a moment.', limit: 'Too many tries. Please wait a minute and try again.', closed: 'This form is not open right now. Please call us instead.' },
      footer: { privacy: 'We use your details only to answer your enquiry and never share them.' },
      contact: { phone: 'Phone', email: 'E-mail' },
    },
    ar: {
      skip: 'انتقل إلى المحتوى', brand: 'برايت فيوتشرز',
      nav: { about: 'من نحن', day: 'يوم في الحضانة', parents: 'أولياء الأمور', faq: 'أسئلة', enquire: 'اسأل عن مكان' },
      hero: { kicker: 'السنوات الأولى في الإمارات', title: 'مكان سعيد وآمن يعرف فيه كل طفل ويُحَب', lead: 'مجموعات صغيرة ومعلمات حنونات ويوم هادئ ومرح. نبقيك قريباً من يوم طفلك، بالعربية والإنجليزية.', cta1: 'احجز زيارة', cta2: 'شاهد يوماً معنا' },
      about: { kicker: 'لماذا تختارنا العائلات', title: 'رعاية تراها وتعلّم تشعر به', c1t: 'معروف ومحبوب', c1: 'لكل طفل معلمة مرافقة تعرف عاداته وما يحبه واحتياجاته.', c2t: 'آمن بالتصميم', c2: 'تسجيل دخول وخروج آمن، وأشخاص محددون للاستلام، وعدد كافٍ من الموظفين في كل غرفة طوال اليوم.', c3t: 'عربي وإنجليزي', c3: 'أغانٍ وقصص وحوارات باللغتين لينمو الأطفال بثقة في كل منهما.', c4t: 'قريب من يوم طفلك', c4: 'صور ووجبات وقيلولة وتقارير يومية في تطبيق أولياء الأمور، مع رسائل إلى الفريق.' },
      day: { kicker: 'يوم معنا', title: 'إيقاع لطيف من اللعب والراحة والاكتشاف', s1t: 'استقبال دافئ', s1: 'نستقبل الأطفال بأسمائهم ويبدؤون باللعب الحر.', s2t: 'وقت الحلقة والقصص', s2: 'أغانٍ ولغات وحكايات لنبدأ اليوم معاً.', s3t: 'لعب وتعلّم', s3: 'أنشطة داخلية وخارجية للحركة والإبداع والمهارات المبكرة.', s4t: 'وجبات صحية وراحة', s4: 'طعام مغذٍّ يراعي احتياجات كل عائلة، ثم راحة هادئة.', s5t: 'العودة مع تقرير يومي', s5: 'تعرفين كيف كان اليوم، مع صور وملاحظات، قبل وصولك.' },
      parents: { kicker: 'لأولياء الأمور', title: 'كل شيء في مكان واحد', c1t: 'تطبيق أولياء الأمور', c1: 'شاهد الخلاصة والتقارير اليومية والرسائل، واختر التنبيهات التي تصلك.', c2t: 'الفواتير والنماذج', c2: 'اطّلع على الفواتير واملأ النماذج واقرأ النشرات دون ورق.', c3t: 'خصوصيتك', c3: 'الصور والتفاصيل تُشارَك مع عائلتك فقط. نلتزم بقواعد حماية البيانات في الإمارات.', portal: 'دخول أولياء الأمور' },
      faq: { kicker: 'أسئلة', title: 'ما تسأل عنه العائلات غالباً', q1: 'هل يمكنني الزيارة قبل أن أقرر؟', a1: 'نعم. أرسل لنا النموذج أدناه وسننظم جولة في وقت يناسبك.', q2: 'ما الأعمار التي تقبلونها؟', a2: 'اكتب تاريخ ميلاد طفلك في النموذج وسنوضح لك المجموعة المناسبة.', q3: 'كيف أعرف أن طفلي بأمان؟', a3: 'يُسجَّل دخول الأطفال وخروجهم بأمان، ولا يستلمهم إلا الأشخاص المحددون، ونحافظ على عدد كافٍ من الموظفين في كل غرفة.', q4: 'ماذا عن الرسوم؟', a4: 'تعتمد الرسوم على البرنامج والأيام. سنرسل لك تفصيلاً واضحاً عند استفسارك.' },
      form: { kicker: 'اسأل عن مكان', title: 'حدّثنا عن طفلك', note: 'سنعود إليك قريباً. لا يرى ما ترسله إلا فريقنا.', family: 'اسمك', child: 'اسم الطفل', phone: 'الهاتف', email: 'البريد الإلكتروني', dob: 'تاريخ ميلاد الطفل', start: 'تاريخ البدء المطلوب', contact: 'كيف نتواصل معك؟', byPhone: 'الهاتف', byEmail: 'البريد الإلكتروني', message: 'أي شيء تودّ أن نعرفه', send: 'إرسال', sending: 'جارٍ الإرسال...', needNames: 'يرجى كتابة اسمك واسم طفلك.', needContact: 'يرجى كتابة رقم هاتف أو بريد إلكتروني لنتمكن من الوصول إليك.', ok: 'شكراً لك! وصلتنا رسالتك وسنتواصل معك قريباً.', fail: 'عذراً، تعذر الإرسال الآن. يرجى المحاولة بعد قليل.', limit: 'محاولات كثيرة. يرجى الانتظار دقيقة ثم المحاولة.', closed: 'النموذج غير متاح الآن. يرجى الاتصال بنا.' },
      footer: { privacy: 'نستخدم بياناتك للرد على استفسارك فقط ولا نشاركها مع أحد.' },
      contact: { phone: 'الهاتف', email: 'البريد الإلكتروني' },
    },
  }

  var lang = 'en'
  try { lang = localStorage.getItem('site.lang') || ((navigator.language || 'en').slice(0, 2) === 'ar' ? 'ar' : 'en') } catch (e) { lang = 'en' }
  var centreName = null
  var captchaRead = null

  function get(path) { return path.split('.').reduce(function (o, k) { return o && o[k] }, T[lang]) }

  function paint() {
    var rtl = lang === 'ar'
    document.documentElement.lang = lang
    document.documentElement.dir = rtl ? 'rtl' : 'ltr'
    document.querySelectorAll('[data-i18n]').forEach(function (el) { var v = get(el.getAttribute('data-i18n')); if (v) el.textContent = v })
    var name = centreName ? (centreName[lang] || centreName.en || Object.values(centreName)[0]) : T[lang].brand
    document.getElementById('brandName').textContent = name
    document.getElementById('footName').textContent = name
    document.title = name
    var b = document.getElementById('lang')
    b.textContent = rtl ? 'English' : 'العربية'
    b.setAttribute('lang', rtl ? 'en' : 'ar')
    var parts = []
    if (cfg.phone) parts.push(T[lang].contact.phone + ': ' + cfg.phone)
    if (cfg.email) parts.push(T[lang].contact.email + ': ' + cfg.email)
    if (cfg.address && (cfg.address[lang] || cfg.address.en)) parts.push(cfg.address[lang] || cfg.address.en)
    document.getElementById('contactLine').textContent = parts.join(' · ')
    var portal = document.getElementById('portal')
    if (cfg.portalUrl) { portal.hidden = false; portal.href = cfg.portalUrl }
  }

  function say(kind, text) {
    var box = document.getElementById('status')
    box.innerHTML = ''
    if (!text) return
    var p = document.createElement('p')
    p.className = 'msg ' + kind
    p.textContent = text
    box.appendChild(p)
    box.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  }

  function loadCaptcha(siteKey) {
    var s = document.createElement('script')
    s.src = 'https://www.google.com/recaptcha/api.js?render=explicit'
    s.async = true
    s.onload = function () {
      setTimeout(function () {
        if (!window.grecaptcha) return
        var id = window.grecaptcha.render(document.getElementById('captchaBox'), { sitekey: siteKey })
        captchaRead = function () { return window.grecaptcha.getResponse(id) }
      }, 300)
    }
    document.head.appendChild(s)
  }

  var closed = false
  fetch(base + '/enquiry-form/' + encodeURIComponent(cfg.centreSlug || ''))
    .then(function (r) { return r.json() })
    .then(function (j) {
      if (j && j.success && j.data) {
        centreName = j.data.name
        if (j.data.captcha_site_key) loadCaptcha(j.data.captcha_site_key)
      } else { closed = true }
      paint()
    })
    .catch(function () { closed = true })

  document.getElementById('lang').addEventListener('click', function () {
    lang = lang === 'en' ? 'ar' : 'en'
    try { localStorage.setItem('site.lang', lang) } catch (e) { /* the choice just is not remembered */ }
    paint()
  })

  document.getElementById('enquiryForm').addEventListener('submit', function (ev) {
    ev.preventDefault()
    var f = ev.target
    var v = function (n) { return (f.elements[n].value || '').trim() }
    if (!v('family_name') || !v('child_name')) return say('err', get('form.needNames'))
    if (!v('phone') && !v('email')) return say('err', get('form.needContact'))
    if (closed) return say('err', get('form.closed'))
    var btn = document.getElementById('send')
    btn.disabled = true
    btn.textContent = get('form.sending')
    say('', '')
    fetch(base + '/enquiries/' + encodeURIComponent(cfg.centreSlug), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        family_name: v('family_name'), child_name: v('child_name'), phone: v('phone') || null, email: v('email') || null,
        preferred_contact: v('preferred_contact'), child_date_of_birth: v('child_date_of_birth') || null,
        desired_start_date: v('desired_start_date') || null, message: v('message') || null,
        website: f.elements.website.value, captcha_token: captchaRead ? captchaRead() || null : null,
      }),
    }).then(function (r) {
      if (r.status === 429) { say('err', get('form.limit')); return }
      return r.json().then(function (j) {
        if (j && j.success) { f.reset(); say('ok', get('form.ok')) } else { say('err', (j && j.errors && j.errors[0] && j.errors[0].message) || get('form.fail')) }
      })
    }).catch(function () { say('err', get('form.fail')) }).then(function () {
      btn.disabled = false
      btn.textContent = get('form.send')
    })
  })

  paint()
})()
