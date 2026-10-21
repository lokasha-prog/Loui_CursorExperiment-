/* Nursery website: English/Arabic text, centre name and programmes from the NMS server, animations, and the enquiry form that sends to the centre's pipeline. */
(function () {
  'use strict'
  var cfg = window.SITE_CONFIG || {}
  var base = (cfg.apiBase || '') + '/api/v1/public'
  var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches

  var T = {
    en: {
      skip: 'Skip to content', brand: 'Bright Futures',
      nav: { why: 'Why us', programmes: 'Programmes', day: 'A day with us', learning: 'Learning', safety: 'Safety', faq: 'Questions', enquire: 'Book a visit' },
      hero: { kicker: 'Early years in the UAE', t1: 'A happy, safe place where every child is', words: ['known', 'loved', 'curious', 'confident', 'learning'], lead: 'Small groups, caring teachers and a calm, playful day. We keep you close to your child\'s day, and to their learning, in English and Arabic.', cta1: 'Book a visit', cta2: 'See how children learn here' },
      marquee: ['Play-based learning', 'English + Arabic', 'Secure check-in', 'Learning journey', 'Parent app', 'Healthy meals', 'Small groups', 'Caring key person', 'Daily reports', 'Early Years Foundation Stage'],
      badge: { safe: 'Secure check-in', bi: 'English + Arabic', learn: 'Learning journey', app: 'Parent app', ratio: 'Never more than {n} children per adult' },
      why: { kicker: 'Why families choose us', title: 'Care you can see, learning you can feel', lead: 'Choosing a nursery is one of the biggest decisions you make. Here is what you can expect from us, every single day.', c1t: 'Known and loved', c1: 'Every child has a key person who knows their routines, favourite songs, comfort habits and needs.', c2t: 'Safe by design', c2: 'Secure check-in and check-out, named pick-up people only, and the right number of adults in every room.', c3t: 'English and Arabic', c3: 'Songs, stories and daily conversations in both languages, so children grow confident in each.', c4t: 'Learning you can follow', c4: 'We record what your child does and learns, and share it with you in a learning journey and termly reports.', c5t: 'Healthy and mindful', c5: 'Nutritious meals that respect every family\'s needs, allergy care taken seriously, and rest when little bodies need it.', c6t: 'Close to your child\'s day', c6: 'Photos, meals, naps and a daily report in our parent app, with direct messages to the team.' },
      prog: { kicker: 'Our programmes', title: 'A place for every age', lead: 'Each group has its own space, routine and teachers, so your child gets what suits their stage.', days: '{n} days a week', hours: '{a} to {b}', age: 'Ages {a} to {b}', m: 'months', y: 'years', fProg: 'programmes', fRooms: 'rooms', fRatio: 'children per adult at most', fLang: 'languages' },
      day: { kicker: 'A day with us', title: 'A gentle rhythm of play, rest and discovery', lead: 'Children thrive on routine. Our day flows from calm arrival to joyful play, healthy meals and quiet rest, and you hear all about it before pick-up.', cta: 'Come and see it', s1t: 'Warm welcome', s1: 'Children are greeted by name, settle in and start with free play.', s2t: 'Circle time and stories', s2: 'Songs, languages, rhymes and storytelling to begin the day together.', s3t: 'Play and learning', s3: 'Planned lessons, art, building, sensory play, movement and outdoor exploration.', s4t: 'Healthy meals and rest', s4: 'Nutritious food, then a quiet rest or calm activities for each child\'s need.', s5t: 'Home with a daily report', s5: 'You see photos and notes from the day, so there is always something to talk about.', times: ['Morning arrival', 'Circle time', 'Learning and play', 'Lunch and rest', 'Home time'] },
      learn: { kicker: 'How children learn here', title: 'Learning you can watch grow', lead: 'We follow the Early Years Foundation Stage, the framework used by the best early years settings. Every child\'s learning is planned, observed, assessed and shared with you.', a1: 'Communication and language', a1d: 'Listening, talking, stories', a2: 'Physical development', a2d: 'Moving, balance, fine skills', a3: 'Personal, social and emotional', a3d: 'Friends, feelings, confidence', a4: 'Literacy', a4d: 'Books, sounds, first writing', a5: 'Mathematics', a5d: 'Numbers, shapes, patterns', a6: 'Understanding the world', a6d: 'Nature, people, our community', a7: 'Expressive arts and design', a7d: 'Music, art, imagination' },
      loop: { title: 'How we follow your child\'s learning', lead: 'Four steps, repeated all year, so nothing about your child\'s progress is left to chance.', s1t: 'Plan', s1: 'Teachers plan lessons for each age group and room, linked to the areas of learning and the milestones we want to reach.', s2t: 'Observe', s2: 'We watch and note what each child does and says, with the milestone it shows. Quick notes and photos, every week.', s3t: 'Assess', s3: 'Each term, a checklist for your child\'s age shows what is secure, emerging or still to come.', s4t: 'Share', s4: 'A manager checks a plain-language report before you see it. You read it in the app, in English or Arabic.', promise: 'Every report is read by a teacher before you see it, and the words are kind, plain and honest. If we ever have a worry about your child, we will talk to you in person first.' },
      show: { t1: 'Moments we capture', t2: 'Learning journey', t3: 'Termly report', example: 'Example of what parents see. Names and details are illustrations.', o1c: 'Mathematics', o1: 'Counted seven stones without help and matched them to the number card.', o2c: 'Communication', o2: 'Told the group how she helped a friend put on their shoes.', o3c: 'Creative', o3: 'Mixed blue and yellow paint and shouted: green!', j1t: 'Report ready', j1: 'Autumn term report approved by the manager', j2t: 'Moment', j2: 'First time writing the letters of her name', j3t: 'Moment', j3: 'Climbed the big frame and jumped down proudly', j4t: 'Moment', j4: 'Joined in with the Arabic rhyme at circle time', sum: 'Layla is growing in confidence. She loves stories and is starting to join words into sentences in both languages.', l1: 'Going strong', l2: 'Growing well', l3: 'Just starting' },
      life: { kicker: 'Life at nursery', title: 'Where curiosity gets to play', p1: 'Messy art mornings', p2: 'Music and movement', p3: 'Garden and outdoor play', p4: 'Story corner', p5: 'Water and sensory play', p6: 'Building and problem solving' },
      safe: { kicker: 'Safety and wellbeing', title: 'Peace of mind, built into every day', lead: 'You should never have to wonder whether your child is safe. These are the things we do, not just promise.', l1: 'Secure check-in and check-out with a scanned card, recorded every time', l2: 'Only the people you name may collect your child, with identity checked', l3: 'Staff-to-child ratios watched live, so every room always has enough adults', l4: 'Allergies and medical needs recorded and visible to the team, even with no signal', l5: 'Photos and details shared only with your own family, under UAE data-protection rules', cardt: 'Your child\'s wellbeing comes first', card: 'From the first conversation we learn about your child\'s health, diet and routines, and the whole team follows them every day.' },
      app: { kicker: 'For parents', title: 'Your child\'s day and learning, in your pocket', lead: 'Our parent app keeps you close, without interrupting your own day.', l1: 'Photos, meals, naps and activities as they happen', l2: 'A daily report every afternoon, in English or Arabic', l5: 'Your child\'s learning journey and approved termly reports', l3: 'Message the team directly and get alerts you choose', l4: 'Invoices, forms and newsletters in one place, no paper', portal: 'Parent sign-in', screenTitle: 'Today', live: 'Live', s1: 'Activity', p1: 'Painted a rainbow and shared the crayons!', s2: 'Lunch', p2: 'Ate all the vegetables today.', s3: 'Nap', p3: 'Slept peacefully for an hour.', learnTitle: 'Layla\'s learning', new: 'New', r1: 'Report ready', r2: 'Autumn term: growing in confidence and loving stories.', m1: 'Counted to ten and sorted shapes.', m2: 'Mixed colours and named them all.', toast1: '🔔 A new learning report is ready', toast2: '📷 New photos from today' },
      voices: { kicker: 'What parents say', title: 'Families who trust us' },
      join: { kicker: 'Joining us', title: 'Three easy steps to a place', s1t: 'Tell us about your child', s1: 'Send the short form below. It takes two minutes and we reply quickly.', s2t: 'Come for a visit', s2: 'Tour the rooms, meet the teachers and ask every question you have.', s3t: 'Settle in gently', s3: 'We plan a gradual start so your child, and you, feel confident from day one.' },
      faq: { kicker: 'Questions', title: 'Things families often ask', q1: 'Can I visit before I decide?', a1: 'Yes, please do. Send us the form below and we will arrange a tour at a time that suits you.', q2: 'What ages do you take?', a2: 'See our programmes above. Tell us your child\'s date of birth in the form and we will explain which group is right.', q3: 'How do I know my child is safe?', a3: 'Children are checked in and out securely, only named people may collect them, and we keep the right number of adults in every room.', q4: 'What about fees?', a4: 'Fees depend on the programme and days. We will send a clear breakdown when you enquire, with nothing hidden.', q5: 'What if my child has allergies or special needs?', a5: 'Tell us everything during your visit. Allergies, medical needs and routines are recorded and followed by the whole team.', q6: 'Do you speak Arabic and English with the children?', a6: 'Yes. Both languages are part of the day, in songs, stories and conversation.', q8: 'How do you track my child\'s learning?', a8: 'Teachers note what your child does each week, linked to the areas of learning. Each term we complete an age-appropriate checklist. You see the moments you are shared and a plain-language termly report.', q9: 'Who sees the reports before I do?', a9: 'A manager reads and approves every family report first. Worries are never put in the app: we speak to you personally.', q10: 'What if my child needs extra support?', a10: 'We notice early, talk with you, and plan together. Teachers record concerns privately so the right support starts quickly.' },
      form: { kicker: 'Book a visit', title: 'Tell us about your child', note: 'We will get back to you soon. Only our team sees what you send.', family: 'Your name', child: 'Child\'s name', phone: 'Phone', email: 'E-mail', dob: 'Child\'s date of birth', start: 'Wished start date', contact: 'How should we contact you?', byPhone: 'Phone', byEmail: 'E-mail', message: 'Anything you would like us to know', send: 'Send', sending: 'Sending...', needNames: 'Please tell us your name and your child\'s name.', needContact: 'Please give a phone number or an e-mail so we can reach you.', ok: 'Thank you! We have your message and will be in touch soon.', fail: 'Sorry, we could not send this just now. Please try again in a moment.', limit: 'Too many tries. Please wait a minute and try again.', closed: 'This form is not open right now. Please call us instead.' },
      footer: { tag: 'Early years care and learning, in English and Arabic.', privacy: 'We use your details only to answer your enquiry and never share them.', explore: 'Explore', contact: 'Contact' },
      contact: { phone: 'Phone', email: 'E-mail' },
    },
    ar: {
      skip: 'انتقل إلى المحتوى', brand: 'برايت فيوتشرز',
      nav: { why: 'لماذا نحن', programmes: 'البرامج', day: 'يوم معنا', learning: 'التعلّم', safety: 'الأمان', faq: 'أسئلة', enquire: 'احجز زيارة' },
      hero: { kicker: 'السنوات الأولى في الإمارات', t1: 'مكان سعيد وآمن يكون فيه كل طفل', words: ['معروفاً', 'محبوباً', 'فضولياً', 'واثقاً', 'متعلّماً'], lead: 'مجموعات صغيرة ومعلمات حنونات ويوم هادئ ومرح. نبقيك قريباً من يوم طفلك ومن تعلّمه، بالعربية والإنجليزية.', cta1: 'احجز زيارة', cta2: 'شاهد كيف يتعلم الأطفال هنا' },
      marquee: ['تعلّم باللعب', 'عربي + إنجليزي', 'تسجيل دخول آمن', 'رحلة التعلّم', 'تطبيق أولياء الأمور', 'وجبات صحية', 'مجموعات صغيرة', 'معلمة مرافقة حنونة', 'تقارير يومية', 'مرحلة التأسيس للسنوات المبكرة'],
      badge: { safe: 'تسجيل دخول آمن', bi: 'عربي + إنجليزي', learn: 'رحلة التعلّم', app: 'تطبيق أولياء الأمور', ratio: 'لا يزيد عن {n} أطفال لكل بالغ' },
      why: { kicker: 'لماذا تختارنا العائلات', title: 'رعاية تراها وتعلّم تشعر به', lead: 'اختيار الحضانة من أهم قراراتك. هذا ما يمكنك توقعه منا كل يوم.', c1t: 'معروف ومحبوب', c1: 'لكل طفل معلمة مرافقة تعرف عاداته وأغانيه المفضلة وما يريحه واحتياجاته.', c2t: 'آمن بالتصميم', c2: 'تسجيل دخول وخروج آمن، وأشخاص محددون فقط للاستلام، وعدد كافٍ من البالغين في كل غرفة.', c3t: 'عربي وإنجليزي', c3: 'أغانٍ وقصص وحوارات يومية باللغتين، ليكبر الأطفال بثقة في كل منهما.', c4t: 'تعلّم تستطيع متابعته', c4: 'نسجّل ما يفعله طفلك وما يتعلمه، ونشاركه معك في رحلة تعلّم وتقارير فصلية.', c5t: 'صحي وواعٍ', c5: 'وجبات مغذية تراعي احتياجات كل عائلة، واهتمام جاد بالحساسية، وراحة عندما تحتاجها الأجسام الصغيرة.', c6t: 'قريب من يوم طفلك', c6: 'صور ووجبات وقيلولة وتقرير يومي في تطبيق أولياء الأمور، مع رسائل مباشرة إلى الفريق.' },
      prog: { kicker: 'برامجنا', title: 'مكان لكل عمر', lead: 'لكل مجموعة مساحتها وروتينها ومعلماتها، فيحصل طفلك على ما يناسب مرحلته.', days: '{n} أيام في الأسبوع', hours: 'من {a} إلى {b}', age: 'الأعمار من {a} إلى {b}', m: 'شهراً', y: 'سنوات', fProg: 'برامج', fRooms: 'غرف', fRatio: 'أطفال لكل بالغ كحد أقصى', fLang: 'لغتان' },
      day: { kicker: 'يوم معنا', title: 'إيقاع لطيف من اللعب والراحة والاكتشاف', lead: 'الأطفال يزدهرون بالروتين. يسير يومنا من وصول هادئ إلى لعب مبهج ووجبات صحية وراحة هادئة، وتعرفين كل شيء قبل الاستلام.', cta: 'تعالَ وشاهد', s1t: 'استقبال دافئ', s1: 'نستقبل الأطفال بأسمائهم، ويبدؤون باللعب الحر.', s2t: 'وقت الحلقة والقصص', s2: 'أغانٍ ولغات وأناشيد وحكايات لنبدأ اليوم معاً.', s3t: 'لعب وتعلّم', s3: 'دروس مخططة وفن وبناء ولعب حسي وحركة واستكشاف في الهواء الطلق.', s4t: 'وجبات صحية وراحة', s4: 'طعام مغذٍّ، ثم راحة هادئة أو أنشطة هادئة حسب حاجة كل طفل.', s5t: 'العودة مع تقرير يومي', s5: 'تشاهدين صوراً وملاحظات من اليوم، فيكون هناك دائماً ما تتحدثان عنه.', times: ['وصول الصباح', 'وقت الحلقة', 'تعلّم ولعب', 'الغداء والراحة', 'وقت العودة'] },
      learn: { kicker: 'كيف يتعلم الأطفال هنا', title: 'تعلّم تشاهده ينمو', lead: 'نتبع إطار مرحلة التأسيس للسنوات المبكرة المستخدم في أفضل الحضانات. يُخطَّط لتعلّم كل طفل ويُلاحَظ ويُقيَّم ويُشارَك معك.', a1: 'التواصل واللغة', a1d: 'الاستماع والحديث والقصص', a2: 'النمو الجسدي', a2d: 'الحركة والتوازن والمهارات الدقيقة', a3: 'النمو الشخصي والاجتماعي والعاطفي', a3d: 'الأصدقاء والمشاعر والثقة', a4: 'القراءة والكتابة', a4d: 'الكتب والأصوات وأول الكتابة', a5: 'الرياضيات', a5d: 'الأرقام والأشكال والأنماط', a6: 'فهم العالم', a6d: 'الطبيعة والناس ومجتمعنا', a7: 'الفنون والتعبير', a7d: 'الموسيقى والفن والخيال' },
      loop: { title: 'كيف نتابع تعلّم طفلك', lead: 'أربع خطوات تتكرر طوال العام، فلا يُترك شيء من تقدم طفلك للصدفة.', s1t: 'نخطط', s1: 'تخطط المعلمات دروساً لكل فئة عمرية وغرفة، مرتبطة بمجالات التعلّم والمعالم التي نريد بلوغها.', s2t: 'نلاحظ', s2: 'نراقب ونسجّل ما يفعله كل طفل ويقوله، مع المعلم الذي يظهره. ملاحظات سريعة وصور كل أسبوع.', s3t: 'نقيّم', s3: 'كل فصل دراسي، قائمة مناسبة لعمر طفلك تبين ما تمكّن منه وما بدأ يظهر وما لم يأتِ بعد.', s4t: 'نشارك', s4: 'يراجع المدير تقريراً بلغة بسيطة قبل أن تراه. تقرؤه في التطبيق، بالعربية أو الإنجليزية.', promise: 'تقرأ المعلمة كل تقرير قبل أن تراه، وكلماته لطيفة وبسيطة وصادقة. وإن راودنا أي قلق على طفلك فسنتحدث معك شخصياً أولاً.' },
      show: { t1: 'لحظات نسجّلها', t2: 'رحلة التعلّم', t3: 'التقرير الفصلي', example: 'مثال لما يراه أولياء الأمور. الأسماء والتفاصيل للتوضيح فقط.', o1c: 'الرياضيات', o1: 'عدّت سبع حصى دون مساعدة وطابقتها مع بطاقة الرقم.', o2c: 'التواصل', o2: 'أخبرت المجموعة كيف ساعدت صديقتها في ارتداء حذائها.', o3c: 'إبداع', o3: 'مزجت الطلاء الأزرق والأصفر وصاحت: أخضر!', j1t: 'تقرير جاهز', j1: 'اعتمد المدير تقرير الفصل الخريفي', j2t: 'لحظة', j2: 'أول مرة تكتب فيها حروف اسمها', j3t: 'لحظة', j3: 'تسلقت الهيكل الكبير وقفزت بفخر', j4t: 'لحظة', j4: 'شاركت في النشيد العربي في وقت الحلقة', sum: 'ليلى تنمو بثقة. تحب القصص وتبدأ في ضم الكلمات إلى جمل بالعربية والإنجليزية.', l1: 'متمكّنة', l2: 'تنمو بشكل جيد', l3: 'في البداية' },
      life: { kicker: 'الحياة في الحضانة', title: 'حيث يلعب الفضول', p1: 'صباحات الفن الممتعة', p2: 'الموسيقى والحركة', p3: 'الحديقة واللعب في الهواء الطلق', p4: 'ركن القصص', p5: 'اللعب بالماء واللعب الحسي', p6: 'البناء وحل المشكلات' },
      safe: { kicker: 'الأمان والرعاية', title: 'راحة بال في كل يوم', lead: 'لا ينبغي أن تتساءل يوماً إن كان طفلك بأمان. هذه أشياء نفعلها، لا نعد بها فقط.', l1: 'تسجيل دخول وخروج آمن ببطاقة ممسوحة، يُسجَّل في كل مرة', l2: 'لا يستلم طفلك إلا من تحدّدهم، مع التحقق من الهوية', l3: 'نسب الموظفين إلى الأطفال تُراقَب لحظة بلحظة ليكون في كل غرفة بالغون كفاية', l4: 'الحساسية والاحتياجات الطبية مسجلة ومرئية للفريق حتى بدون إشارة', l5: 'الصور والتفاصيل تُشارَك مع عائلتك فقط وفق قواعد حماية البيانات في الإمارات', cardt: 'سلامة طفلك أولاً', card: 'منذ أول حديث نتعرف على صحة طفلك وغذائه وروتينه، ويتبعها الفريق كله كل يوم.' },
      app: { kicker: 'لأولياء الأمور', title: 'يوم طفلك وتعلّمه في جيبك', lead: 'يبقيك تطبيقنا قريباً دون أن يقاطع يومك.', l1: 'صور ووجبات وقيلولة وأنشطة لحظة حدوثها', l2: 'تقرير يومي كل عصر، بالعربية أو الإنجليزية', l5: 'رحلة تعلّم طفلك وتقاريره الفصلية المعتمدة', l3: 'راسل الفريق مباشرة واختر التنبيهات التي تصلك', l4: 'الفواتير والنماذج والنشرات في مكان واحد، بلا ورق', portal: 'دخول أولياء الأمور', screenTitle: 'اليوم', live: 'مباشر', s1: 'نشاط', p1: 'رسم قوس قزح وشارك الألوان!', s2: 'غداء', p2: 'أكل كل الخضار اليوم.', s3: 'قيلولة', p3: 'نام بهدوء لمدة ساعة.', learnTitle: 'تعلّم ليلى', new: 'جديد', r1: 'تقرير جاهز', r2: 'الفصل الخريفي: تنمو بثقة وتحب القصص.', m1: 'عدّت حتى عشرة ورتّبت الأشكال.', m2: 'مزجت الألوان وسمّتها كلها.', toast1: '🔔 تقرير تعلّم جديد جاهز', toast2: '📷 صور جديدة من اليوم' },
      voices: { kicker: 'ماذا يقول أولياء الأمور', title: 'عائلات تثق بنا' },
      join: { kicker: 'الانضمام إلينا', title: 'ثلاث خطوات سهلة للحصول على مكان', s1t: 'حدّثنا عن طفلك', s1: 'أرسل النموذج القصير أدناه. يستغرق دقيقتين ونرد بسرعة.', s2t: 'تعالَ لزيارة', s2: 'تجوّل في الغرف، وقابل المعلمات، واسأل كل ما لديك من أسئلة.', s3t: 'اعتياد لطيف', s3: 'نخطط لبداية تدريجية ليشعر طفلك وأنت بالثقة منذ اليوم الأول.' },
      faq: { kicker: 'أسئلة', title: 'ما تسأل عنه العائلات غالباً', q1: 'هل يمكنني الزيارة قبل أن أقرر؟', a1: 'نعم بالتأكيد. أرسل لنا النموذج أدناه وسننظم جولة في وقت يناسبك.', q2: 'ما الأعمار التي تقبلونها؟', a2: 'اطّلع على برامجنا أعلاه. اكتب تاريخ ميلاد طفلك في النموذج وسنوضح لك المجموعة المناسبة.', q3: 'كيف أعرف أن طفلي بأمان؟', a3: 'يُسجَّل دخول الأطفال وخروجهم بأمان، ولا يستلمهم إلا الأشخاص المحددون، ونحافظ على عدد كافٍ من البالغين في كل غرفة.', q4: 'ماذا عن الرسوم؟', a4: 'تعتمد الرسوم على البرنامج والأيام. سنرسل لك تفصيلاً واضحاً عند استفسارك دون أي رسوم خفية.', q5: 'ماذا لو كان لدى طفلي حساسية أو احتياجات خاصة؟', a5: 'أخبرنا بكل شيء أثناء الزيارة. الحساسية والاحتياجات الطبية والروتين مسجلة ويتبعها الفريق كله.', q6: 'هل تتحدثون العربية والإنجليزية مع الأطفال؟', a6: 'نعم. اللغتان جزء من اليوم، في الأغاني والقصص والحوار.', q8: 'كيف تتابعون تعلّم طفلي؟', a8: 'تسجّل المعلمات ما يفعله طفلك كل أسبوع، مرتبطاً بمجالات التعلّم. وفي كل فصل نكمل قائمة تقييم مناسبة لعمره. ترى اللحظات التي نشاركها معك وتقريراً فصلياً بلغة بسيطة.', q9: 'من يرى التقارير قبلي؟', a9: 'يقرأ المدير كل تقرير للأسرة ويعتمده أولاً. لا تُوضع المخاوف في التطبيق: نتحدث إليك شخصياً.', q10: 'ماذا لو احتاج طفلي دعماً إضافياً؟', a10: 'نلاحظ مبكراً ونتحدث معك ونخطط معاً. تسجّل المعلمات الملاحظات المقلقة بسرية ليبدأ الدعم المناسب بسرعة.' },
      form: { kicker: 'احجز زيارة', title: 'حدّثنا عن طفلك', note: 'سنعود إليك قريباً. لا يرى ما ترسله إلا فريقنا.', family: 'اسمك', child: 'اسم الطفل', phone: 'الهاتف', email: 'البريد الإلكتروني', dob: 'تاريخ ميلاد الطفل', start: 'تاريخ البدء المطلوب', contact: 'كيف نتواصل معك؟', byPhone: 'الهاتف', byEmail: 'البريد الإلكتروني', message: 'أي شيء تودّ أن نعرفه', send: 'إرسال', sending: 'جارٍ الإرسال...', needNames: 'يرجى كتابة اسمك واسم طفلك.', needContact: 'يرجى كتابة رقم هاتف أو بريد إلكتروني لنتمكن من الوصول إليك.', ok: 'شكراً لك! وصلتنا رسالتك وسنتواصل معك قريباً.', fail: 'عذراً، تعذر الإرسال الآن. يرجى المحاولة بعد قليل.', limit: 'محاولات كثيرة. يرجى الانتظار دقيقة ثم المحاولة.', closed: 'النموذج غير متاح الآن. يرجى الاتصال بنا.' },
      footer: { tag: 'رعاية وتعليم للسنوات المبكرة، بالعربية والإنجليزية.', privacy: 'نستخدم بياناتك للرد على استفسارك فقط ولا نشاركها مع أحد.', explore: 'استكشف', contact: 'تواصل معنا' },
      contact: { phone: 'الهاتف', email: 'البريد الإلكتروني' },
    },
  }

  var lang = 'en'
  try { lang = localStorage.getItem('site.lang') || ((navigator.language || 'en').slice(0, 2) === 'ar' ? 'ar' : 'en') } catch (e) { lang = 'en' }
  var centre = null
  var captchaRead = null
  var closed = false
  var EMOJI = ['🍼', '🧸', '🎨', '🎒', '📚', '🌟']
  var rotIndex = 0

  function get(path) { return path.split('.').reduce(function (o, k) { return o && o[k] }, T[lang]) }
  function fill(text, vars) { return String(text).replace(/\{(\w+)\}/g, function (_, k) { return vars[k] }) }
  function pickText(obj) { return obj ? (obj[lang] || obj.en || Object.values(obj)[0] || '') : '' }
  function nameOf() { return centre ? pickText(centre.name) : T[lang].brand }
  function months(n) { return n < 24 ? n + ' ' + get('prog.m') : (Math.round(n / 12 * 10) / 10) + ' ' + get('prog.y') }
  function el(tag, cls, text) { var e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e }
  var $ = function (id) { return document.getElementById(id) }

  /* ---------- content that depends on the server and the language ---------- */
  function paintProgrammes() {
    var box = $('progs'); var sec = $('programmes'); var facts = $('facts')
    box.innerHTML = ''; facts.innerHTML = ''
    var list = centre && centre.programmes ? centre.programmes : []
    sec.hidden = list.length === 0
    list.forEach(function (p, i) {
      var card = el('div', 'prog rv in')
      card.appendChild(el('span', 'emoji', EMOJI[i % EMOJI.length]))
      card.appendChild(el('span', 'age', fill(get('prog.age'), { a: months(p.min_age_months), b: months(p.max_age_months) })))
      card.appendChild(el('h3', null, pickText(p.name)))
      if (p.description) card.appendChild(el('p', null, pickText(p.description)))
      var ul = el('ul')
      ul.appendChild(el('li', null, '📅 ' + fill(get('prog.days'), { n: p.days_per_week })))
      ul.appendChild(el('li', null, '🕘 ' + fill(get('prog.hours'), { a: p.start_time, b: p.end_time })))
      card.appendChild(ul)
      box.appendChild(card)
    })
    var items = [[list.length, get('prog.fProg')]]
    if (centre && centre.rooms) items.push([centre.rooms, get('prog.fRooms')])
    if (centre && centre.children_per_adult_at_most) items.push([centre.children_per_adult_at_most, get('prog.fRatio')])
    items.push([2, get('prog.fLang')])
    items.forEach(function (it) {
      var f = el('div', 'fact')
      var b = el('b', null, String(it[0])); b.setAttribute('data-count', it[0])
      f.appendChild(b); f.appendChild(el('span', null, it[1]))
      facts.appendChild(f)
    })
    countUpWhenSeen()
  }

  function paintMarquee() {
    var words = get('marquee') || []
    var t = $('marquee'); t.innerHTML = ''
    for (var r = 0; r < 2; r++) words.forEach(function (w) { t.appendChild(el('span', null, '✦ ' + w)) })
  }

  function paintExtras() {
    var n = $('notice')
    if (cfg.notice && pickText(cfg.notice)) {
      n.hidden = false; n.textContent = ''
      var a = el('a', null, pickText(cfg.notice)); a.href = cfg.notice.link || '#enquire'; n.appendChild(a)
    }
    var quotes = cfg.testimonials || []
    $('voices').hidden = quotes.length === 0
    var qbox = $('quotes'); qbox.innerHTML = ''
    quotes.forEach(function (q) {
      var d = el('figure', 'quote rv in'); d.style.margin = '0'
      d.appendChild(el('p', null, '“' + pickText(q.text) + '”')); d.appendChild(el('cite', null, q.name || ''))
      qbox.appendChild(d)
    })
    var photos = cfg.photos || []
    if (photos.length) {
      var g = $('gallery'); g.innerHTML = ''
      photos.forEach(function (p) {
        var s = el('div', 'shot rv in')
        var img = el('img'); img.src = p.src; img.alt = pickText(p.label); img.loading = 'lazy'
        s.appendChild(img); s.appendChild(el('span', 'label', pickText(p.label))); g.appendChild(s)
      })
    }
    var rb = $('ratioBadge')
    if (centre && centre.children_per_adult_at_most) { rb.hidden = false; $('ratioText').textContent = fill(get('badge.ratio'), { n: centre.children_per_adult_at_most }) }
    if (cfg.phone) { $('fab').hidden = false; $('wa').href = 'https://wa.me/' + cfg.phone.replace(/[^\d]/g, '') }
    var ld = { '@context': 'https://schema.org', '@type': 'ChildCare', name: nameOf() }
    if (cfg.phone) ld.telephone = cfg.phone
    if (cfg.email) ld.email = cfg.email
    $('ld').textContent = JSON.stringify(ld)
  }

  function paint() {
    var rtl = lang === 'ar'
    document.documentElement.lang = lang
    document.documentElement.dir = rtl ? 'rtl' : 'ltr'
    document.querySelectorAll('[data-i18n]').forEach(function (e) { var v = get(e.getAttribute('data-i18n')); if (typeof v === 'string') e.textContent = v })
    var name = nameOf()
    $('brandName').textContent = name; $('footName').textContent = name; document.title = name
    var b = $('lang'); b.textContent = rtl ? 'English' : 'العربية'; b.setAttribute('lang', rtl ? 'en' : 'ar')
    var parts = []
    if (cfg.phone) parts.push(T[lang].contact.phone + ': ' + cfg.phone)
    if (cfg.email) parts.push(T[lang].contact.email + ': ' + cfg.email)
    if (cfg.address && (cfg.address[lang] || cfg.address.en)) parts.push(cfg.address[lang] || cfg.address.en)
    $('contactLine').textContent = parts.join(' · ')
    var portal = $('portal')
    if (cfg.portalUrl) { portal.hidden = false; portal.href = cfg.portalUrl }
    $('rot').textContent = (get('hero.words') || [''])[rotIndex % get('hero.words').length]
    paintProgrammes(); paintMarquee(); paintExtras(); staggerAll(); sunStep()
  }

  /* ---------- animation helpers ---------- */
  function staggerAll() {
    document.querySelectorAll('.cards, .progs, .areas, .loop, .gallery, .steps3, .checks, .timeline').forEach(function (group) {
      Array.prototype.forEach.call(group.children, function (child, i) { child.style.setProperty('--stagger', (i * 0.09) + 's') })
    })
  }

  var io = null
  function observeReveals() {
    var targets = document.querySelectorAll('.rv, #tree, .panel')
    if (!('IntersectionObserver' in window) || reduce) { targets.forEach(function (n) { n.classList.add('in') }); return }
    io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) { if (en.isIntersecting) { en.target.classList.add('in'); io.unobserve(en.target) } })
    }, { threshold: 0.14 })
    document.querySelectorAll('.rv, #tree').forEach(function (n) { io.observe(n) })
  }

  function countUpWhenSeen() {
    var nums = document.querySelectorAll('[data-count]')
    nums.forEach(function (n) {
      var target = Number(n.getAttribute('data-count'))
      if (reduce || !('IntersectionObserver' in window)) { n.textContent = target; return }
      n.textContent = '0'
      var o = new IntersectionObserver(function (es) {
        if (!es[0].isIntersecting) return
        o.disconnect()
        var start = performance.now()
        var tick = function (t) {
          var p = Math.min(1, (t - start) / 1100)
          n.textContent = Math.round(target * (1 - Math.pow(1 - p, 3)))
          if (p < 1) requestAnimationFrame(tick)
        }
        requestAnimationFrame(tick)
      }, { threshold: 0.6 })
      o.observe(n)
    })
  }

  // word rotator in the headline
  setInterval(function () {
    if (reduce) return
    var r = $('rot'); var words = get('hero.words'); if (!words) return
    r.classList.add('out')
    setTimeout(function () { rotIndex = (rotIndex + 1) % words.length; r.textContent = words[rotIndex]; r.classList.remove('out') }, 380)
  }, 2600)

  // scroll: progress bar, header shadow, active menu link, moving sun, active timeline step
  var ticking = false
  function onScroll() {
    if (ticking) return
    ticking = true
    requestAnimationFrame(function () {
      ticking = false
      var h = document.documentElement
      var p = h.scrollTop / Math.max(1, h.scrollHeight - h.clientHeight)
      $('progress').style.transform = 'scaleX(' + p + ')'
      $('topbar').classList.toggle('scrolled', h.scrollTop > 8)
      var current = ''
      document.querySelectorAll('main section[id]').forEach(function (s) { var r = s.getBoundingClientRect(); if (r.top < 140 && r.bottom > 140) current = s.id })
      document.querySelectorAll('nav.main a.link').forEach(function (a) { a.classList.toggle('on', a.getAttribute('href') === '#' + current) })
      sunStep()
    })
  }
  window.addEventListener('scroll', onScroll, { passive: true })
  window.addEventListener('resize', onScroll)

  function sunStep() {
    var tl = $('timeline'); var sky = $('sky'); if (!tl || !sky) return
    var steps = tl.querySelectorAll('.tl')
    var mid = window.innerHeight * 0.55
    var active = 0; var best = Infinity
    steps.forEach(function (s, i) { var r = s.getBoundingClientRect(); var d = Math.abs(r.top + r.height / 2 - mid); if (d < best) { best = d; active = i } })
    steps.forEach(function (s, i) { s.classList.toggle('on', i === active) })
    var prog = steps.length > 1 ? active / (steps.length - 1) : 0
    var w = sky.clientWidth; var h = sky.clientHeight
    var x = 10 + prog * (w - 66)
    var y = h - 52 - Math.sin(Math.PI * prog) * (h - 70)
    $('sunball').style.transform = 'translate(' + x + 'px,' + y + 'px)'
    var skies = [['#ffd9b8', '#fff3e6'], ['#bfe3f6', '#fff3e6'], ['#8fd0f2', '#e4f5ff'], ['#bfe3f6', '#ffe9c2'], ['#f9b79a', '#ffe1ae']]
    sky.style.setProperty('--skya', skies[active][0]); sky.style.setProperty('--skyb', skies[active][1])
    var times = get('day.times'); if (times) $('skylabel').textContent = times[active]
  }

  // gentle 3D tilt on cards and a mouse parallax on the hero scene
  if (!reduce && window.matchMedia('(hover: hover)').matches) {
    document.addEventListener('mousemove', function (e) {
      var c = e.target.closest && e.target.closest('.tilt')
      document.querySelectorAll('.tilt.live').forEach(function (n) { if (n !== c) { n.classList.remove('live'); n.style.transform = '' } })
      if (c) {
        var r = c.getBoundingClientRect()
        var x = (e.clientX - r.left) / r.width - 0.5; var y = (e.clientY - r.top) / r.height - 0.5
        c.classList.add('live'); c.style.transform = 'perspective(700px) rotateX(' + (-y * 7) + 'deg) rotateY(' + (x * 9) + 'deg) translateY(-4px)'
      }
      var art = document.querySelector('.hero-art')
      if (art && window.scrollY < 700) {
        var px = (e.clientX / window.innerWidth - 0.5) * 12; var py = (e.clientY / window.innerHeight - 0.5) * 10
        art.style.transform = 'translate(' + px + 'px,' + py + 'px)'
      }
    })
  }

  // learning tree: hover a topic and its leaf pops
  document.querySelectorAll('.area').forEach(function (a) {
    var n = a.getAttribute('data-n')
    var leaf = document.querySelector('.leaf[data-n="' + n + '"]')
    function on() { a.classList.add('hot'); if (leaf) leaf.classList.add('hot') }
    function off() { a.classList.remove('hot'); if (leaf) leaf.classList.remove('hot') }
    a.addEventListener('mouseenter', on); a.addEventListener('mouseleave', off); a.addEventListener('focus', on); a.addEventListener('blur', off)
  })

  // learning examples: tabs
  var tabs = document.querySelectorAll('#stabs [role=tab]')
  function showTab(tab) {
    tabs.forEach(function (t) {
      var on = t === tab
      t.setAttribute('aria-selected', on ? 'true' : 'false'); t.tabIndex = on ? 0 : -1
      var panel = $(t.getAttribute('aria-controls'))
      panel.hidden = !on
      panel.classList.remove('show')
      if (on) { void panel.offsetWidth; requestAnimationFrame(function () { panel.classList.add('show') }) }
    })
  }
  tabs.forEach(function (t, i) {
    t.addEventListener('click', function () { showTab(t) })
    t.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') { var next = tabs[(i + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length]; next.focus(); showTab(next) }
    })
  })
  showTab(tabs[0])

  // phone: flip between today and learning every few seconds
  var phone = document.querySelector('.phone')
  if (phone && !reduce) setInterval(function () { phone.classList.toggle('flip') }, 5200)

  // burger menu
  var burger = $('burger'); var nav = $('nav')
  burger.addEventListener('click', function () { var open = nav.classList.toggle('open'); burger.setAttribute('aria-expanded', open ? 'true' : 'false') })
  nav.addEventListener('click', function (e) { if (e.target.tagName === 'A') { nav.classList.remove('open'); burger.setAttribute('aria-expanded', 'false') } })

  $('lang').addEventListener('click', function () {
    lang = lang === 'en' ? 'ar' : 'en'
    try { localStorage.setItem('site.lang', lang) } catch (e) { /* the choice just is not remembered */ }
    paint()
  })

  /* ---------- confetti when an enquiry is sent ---------- */
  function confetti() {
    if (reduce) return
    var c = $('confetti'); var ctx = c.getContext('2d')
    c.width = window.innerWidth; c.height = window.innerHeight
    var colors = ['#f6c453', '#b13f25', '#1f7a6d', '#cfeaf7', '#fcd9d4', '#e3dcf8']
    var bits = []
    for (var i = 0; i < 140; i++) bits.push({ x: c.width / 2 + (Math.random() - .5) * 200, y: c.height * 0.6, vx: (Math.random() - .5) * 14, vy: -Math.random() * 16 - 4, s: 5 + Math.random() * 7, r: Math.random() * 6, vr: (Math.random() - .5) * .4, c: colors[i % colors.length] })
    var t0 = performance.now()
    function frame(t) {
      ctx.clearRect(0, 0, c.width, c.height)
      bits.forEach(function (b) { b.vy += .35; b.x += b.vx; b.y += b.vy; b.r += b.vr; ctx.save(); ctx.translate(b.x, b.y); ctx.rotate(b.r); ctx.fillStyle = b.c; ctx.fillRect(-b.s / 2, -b.s / 2, b.s, b.s * .6); ctx.restore() })
      if (t - t0 < 2600) requestAnimationFrame(frame); else ctx.clearRect(0, 0, c.width, c.height)
    }
    requestAnimationFrame(frame)
  }

  /* ---------- server data and the form ---------- */
  function say(kind, text) {
    var box = $('status'); box.innerHTML = ''
    if (!text) return
    var p = document.createElement('p'); p.className = 'msg ' + kind; p.textContent = text; box.appendChild(p)
  }

  function loadCaptcha(siteKey) {
    var s = document.createElement('script')
    s.src = 'https://www.google.com/recaptcha/api.js?render=explicit'; s.async = true
    s.onload = function () {
      setTimeout(function () {
        if (!window.grecaptcha) return
        var id = window.grecaptcha.render($('captchaBox'), { sitekey: siteKey })
        captchaRead = function () { return window.grecaptcha.getResponse(id) }
      }, 300)
    }
    document.head.appendChild(s)
  }

  fetch(base + '/centre/' + encodeURIComponent(cfg.centreSlug || ''))
    .then(function (r) { return r.json() })
    .then(function (j) { if (j && j.success && j.data) { centre = j.data } else { closed = true } paint() })
    .catch(function () { closed = true })
  fetch(base + '/enquiry-form/' + encodeURIComponent(cfg.centreSlug || ''))
    .then(function (r) { return r.json() })
    .then(function (j) { if (j && j.success && j.data && j.data.captcha_site_key) loadCaptcha(j.data.captcha_site_key) })
    .catch(function () { /* the form still works with the other guards */ })

  $('enquiryForm').addEventListener('submit', function (ev) {
    ev.preventDefault()
    var f = ev.target
    var v = function (n) { return (f.elements[n].value || '').trim() }
    if (!v('family_name') || !v('child_name')) return say('err', get('form.needNames'))
    if (!v('phone') && !v('email')) return say('err', get('form.needContact'))
    if (closed) return say('err', get('form.closed'))
    var btn = $('send'); btn.disabled = true; btn.textContent = get('form.sending'); say('', '')
    fetch(base + '/enquiries/' + encodeURIComponent(cfg.centreSlug), {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        family_name: v('family_name'), child_name: v('child_name'), phone: v('phone') || null, email: v('email') || null,
        preferred_contact: v('preferred_contact'), child_date_of_birth: v('child_date_of_birth') || null,
        desired_start_date: v('desired_start_date') || null, message: v('message') || null,
        website: f.elements.website.value, captcha_token: captchaRead ? captchaRead() || null : null,
      }),
    }).then(function (r) {
      if (r.status === 429) { say('err', get('form.limit')); return }
      return r.json().then(function (j) {
        if (j && j.success) { f.reset(); say('ok', get('form.ok')); confetti() } else { say('err', (j && j.errors && j.errors[0] && j.errors[0].message) || get('form.fail')) }
      })
    }).catch(function () { say('err', get('form.fail')) }).then(function () { btn.disabled = false; btn.textContent = get('form.send') })
  })

  paint()
  observeReveals()
  onScroll()
})()
