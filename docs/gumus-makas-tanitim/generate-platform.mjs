import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import PptxGenJS from 'pptxgenjs';
import {
  Document,
  Packer,
  Paragraph,
  TextRun,
  HeadingLevel,
  AlignmentType,
  ImageRun,
  PageBreak,
} from 'docx';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = __dirname;

const NAVY = '173D62';
const GOLD = 'C9A227';
const DARK = '1A1A1A';
const GRAY = '555555';

const ASSETS =
  'C:\\Users\\yazilimciemir\\.cursor\\projects\\c-Users-yazilimciemir-source-repos-HairDresser-master\\assets';

const SOURCE_MAP = {
  hizmetler:
    'c__Users_yazilimciemir_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_WhatsApp_Image_2026-06-23_at_01.21.44__4_-d3f88e93-4be4-4a75-a371-06a729864029.png',
  randevu:
    'c__Users_yazilimciemir_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_WhatsApp_Image_2026-06-23_at_01.21.44__3_-00cbae47-532f-4c5c-aee4-699bc80da5de.png',
  mesajlasma:
    'c__Users_yazilimciemir_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_WhatsApp_Image_2026-06-23_at_01.21.44__2_-c8736e80-d468-4efa-adb5-fc30ea2ef6c0.png',
  sosyal:
    'c__Users_yazilimciemir_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_WhatsApp_Image_2026-06-23_at_01.21.44__5_-5749ae8f-59bd-458f-867d-566256affa27.png',
  harita:
    'c__Users_yazilimciemir_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_WhatsApp_Image_2026-06-23_at_01.21.44__1_-f6e98b75-357e-402f-bcc6-80abd5fa6c46.png',
};

const IMAGES = {
  kapak: path.join(__dirname, 'kapak.png'),
  hizmetler: path.join(ASSETS, SOURCE_MAP.hizmetler),
  randevu: path.join(ASSETS, SOURCE_MAP.randevu),
  mesajlasma: path.join(ASSETS, SOURCE_MAP.mesajlasma),
  sosyal: path.join(ASSETS, SOURCE_MAP.sosyal),
  harita: path.join(ASSETS, SOURCE_MAP.harita),
};

function requireImage(p) {
  if (!fs.existsSync(p)) throw new Error(`Görsel bulunamadı: ${p}`);
  return p;
}

const PLATFORM_FEATURES = [
  {
    title: 'Yapay Zekâ Destekli Akıllı Platform',
    text: 'Kullanıcı alışkanlıklarını analiz eden, en uygun hizmeti, çalışanı ve işletmeyi öneren yapay zekâ destekli akıllı yönlendirme sistemi.',
    image: null,
  },
  {
    title: 'Uygulama İçi Sosyal Medya Paneli',
    text: 'İşletmelerin ve serbest çalışanların kampanya, çalışma örnekleri, video ve görsellerini paylaşabileceği; kullanıcıların etkileşim kurabileceği sosyal medya altyapısı.',
    image: 'sosyal',
  },
  {
    title: 'Anlık Mesajlaşma Sistemi',
    text: 'WhatsApp benzeri güvenli mesajlaşma özelliği sayesinde müşteri, işletme ve serbest çalışanlar uygulama dışına çıkmadan iletişim kurabilir; fotoğraf ve konum paylaşabilir.',
    image: 'mesajlasma',
  },
  {
    title: 'Haritada Keşfet',
    text: 'Yakındaki işletmeleri ve serbest çalışanları harita üzerinden anlık görüntüleme, tek tuşla iletişim kurma ve filtreleme imkânı.',
    image: 'harita',
  },
  {
    title: 'Alışveriş Modülü',
    text: 'Kullanıcıların bütün ürünlere, kampanyalara ve hizmetlere tek tıkla ulaşabileceği entegre alışveriş sistemi.',
    image: null,
  },
  {
    title: 'Çoklu Profil Kullanımı',
    text: 'Tek hesap üzerinden müşteri, işletme ve serbest çalışan profilleri arasında hızlı geçiş yapabilme özelliği.',
    image: null,
  },
  {
    title: 'Çoklu Dil Desteği',
    text: 'İlk aşamada 4 dil desteği ile yerli ve yabancı kullanıcıların platformu kolayca kullanabilmesini sağlayan global altyapı.',
    image: null,
  },
  {
    title: 'Akıllı Randevu Kombinasyon Sistemi',
    text: 'Yoğunluk, konum, uygunluk ve kullanıcı tercihlerini analiz ederek en doğru işletme ve profesyoneli otomatik eşleştiren bildirim destekli akıllı sistem.',
    image: 'randevu',
  },
];

const FUTURE_ITEMS = [
  {
    title: 'Risk Yönetimi ve Sürdürülebilirlik',
    text: 'Platformun kesintisiz hizmet verebilmesi amacıyla, risk değerlendirme ekibi tarafından riskleri en aza indirmeyi hedefleyen kapsamlı bir B Planı oluşturulmuştur.',
  },
  {
    title: 'C2C Pazaryeri Entegrasyonu',
    text: 'Gümüş Makas ekosistemini tamamlayacak ve kullanıcı deneyimini güçlendirecek şekilde, platforma entegre bir C2C (Consumer to Consumer) Pazaryeri altyapısı planlanmaktadır.',
  },
  {
    title: 'Lojistik Entegrasyonu',
    text: 'Gümüş Makas bünyesinde planlanan C2C Pazaryeri altyapısını desteklemek amacıyla, ürünlerin güvenli, hızlı ve takip edilebilir şekilde taşınmasını sağlayacak lojistik API entegrasyonu hayata geçirilecektir.',
  },
];

const VISION =
  'Vizyonumuz; randevu, iletişim, iş yönetimi, alışveriş, lojistik ve pazaryeri hizmetlerini tek uygulamada buluşturarak sektörün dijital dönüşümüne liderlik eden kapsamlı bir teknoloji ekosistemi oluşturmak.';

function addHeaderBar(slide, pptx, title, subtitle) {
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: '100%', h: 0.12, fill: { color: NAVY } });
  slide.addText(title, {
    x: 0.5,
    y: 0.3,
    w: 9,
    h: 0.55,
    fontSize: 26,
    bold: true,
    color: NAVY,
  });
  if (subtitle) {
    slide.addText(subtitle, {
      x: 0.5,
      y: 0.85,
      w: 9,
      h: 0.35,
      fontSize: 13,
      color: GRAY,
      italic: true,
    });
  }
}

function addFeatureSlide(pptx, item, imgKey) {
  const slide = pptx.addSlide();
  addHeaderBar(slide, pptx, item.title);

  const hasImg = imgKey && IMAGES[imgKey];
  const textW = hasImg ? 4.8 : 8.8;

  slide.addText(item.text, {
    x: 0.55,
    y: 1.35,
    w: textW,
    h: 4.5,
    fontSize: 15,
    color: DARK,
    lineSpacing: 24,
    valign: 'top',
  });

  if (hasImg) {
    slide.addImage({
      path: requireImage(IMAGES[imgKey]),
      x: 5.55,
      y: 1.2,
      w: 3.9,
      h: 4.6,
      sizing: { type: 'contain', w: 3.9, h: 4.6 },
      shadow: { type: 'outer', blur: 6, offset: 2, angle: 45, color: '000000', opacity: 0.2 },
    });
  }
}

async function buildPptx() {
  const pptx = new PptxGenJS();
  pptx.author = 'Yavuzan Teknoloji ve Ticaret Ltd. Şti.';
  pptx.title = 'Gümüş Makas Platform Özellikleri';
  pptx.layout = 'LAYOUT_16x9';

  // Kapak
  const s1 = pptx.addSlide();
  s1.background = { color: 'F5F5F5' };
  s1.addImage({
    path: requireImage(IMAGES.kapak),
    x: 0,
    y: 0,
    w: '100%',
    h: '100%',
    sizing: { type: 'cover', w: '100%', h: '100%' },
  });
  s1.addShape(pptx.ShapeType.rect, {
    x: 0,
    y: 4.6,
    w: '100%',
    h: 1.0,
    fill: { color: NAVY, transparency: 15 },
  });
  s1.addText('Platform Özellikleri & Vizyon', {
    x: 0.5,
    y: 4.75,
    w: 9,
    h: 0.5,
    fontSize: 28,
    bold: true,
    color: 'FFFFFF',
    align: 'center',
  });

  // Genel bakış
  const s2 = pptx.addSlide();
  addHeaderBar(s2, pptx, 'Gelişmiş Platform Özellikleri', 'Yavuzan Teknoloji ve Ticaret Ltd. Şti.');
  s2.addText(
    PLATFORM_FEATURES.map((f) => ({ text: f.title, options: { bullet: true } })),
    {
      x: 0.6,
      y: 1.35,
      w: 8.8,
      h: 4.8,
      fontSize: 15,
      color: DARK,
      lineSpacing: 26,
    },
  );

  // Özellik slaytları
  for (const item of PLATFORM_FEATURES) {
    addFeatureSlide(pptx, item, item.image);
  }

  // İşletme / hizmet ekranı
  const sBiz = pptx.addSlide();
  addHeaderBar(sBiz, pptx, 'İşletme Profili & Hizmet Yönetimi', 'Hizmet paketleri ve fiyatlandırma tek ekranda');
  sBiz.addText(
    'İşletmeler hizmet ve paketlerini uygulama üzerinden tanımlar; müşteriler işletme profilinden tüm hizmetleri görüntüleyerek kolayca randevu alabilir.',
    { x: 0.55, y: 1.35, w: 4.8, h: 2.5, fontSize: 15, color: DARK, lineSpacing: 22 },
  );
  sBiz.addImage({
    path: requireImage(IMAGES.hizmetler),
    x: 5.55,
    y: 1.15,
    w: 3.9,
    h: 4.7,
    sizing: { type: 'contain', w: 3.9, h: 4.7 },
    shadow: { type: 'outer', blur: 6, offset: 2, angle: 45, color: '000000', opacity: 0.2 },
  });

  // Gelecek planları
  const sFuture = pptx.addSlide();
  addHeaderBar(sFuture, pptx, 'Risk Yönetimi & Gelecek Planları');
  let fy = 1.25;
  FUTURE_ITEMS.forEach((item, i) => {
    sFuture.addText(`${i + 1}. ${item.title}`, {
      x: 0.55,
      y: fy,
      w: 8.8,
      h: 0.35,
      fontSize: 16,
      bold: true,
      color: NAVY,
    });
    sFuture.addText(item.text, {
      x: 0.75,
      y: fy + 0.38,
      w: 8.5,
      h: 0.9,
      fontSize: 13,
      color: DARK,
      lineSpacing: 18,
    });
    fy += 1.35;
  });

  // Vizyon
  const sVision = pptx.addSlide();
  sVision.background = { color: NAVY };
  sVision.addText('Vizyonumuz', {
    x: 0.5,
    y: 1.2,
    w: 9,
    h: 0.6,
    fontSize: 32,
    bold: true,
    color: GOLD,
    align: 'center',
  });
  sVision.addText(VISION, {
    x: 0.75,
    y: 2.1,
    w: 8.5,
    h: 3.5,
    fontSize: 18,
    color: 'FFFFFF',
    lineSpacing: 30,
    align: 'center',
    valign: 'mid',
  });

  // Ekran galerisi
  const sGallery = pptx.addSlide();
  addHeaderBar(sGallery, pptx, 'Uygulama Ekranları', 'Gümüş Makas mobil deneyimi');
  const gallery = [
    { key: 'harita', x: 0.4, y: 1.25 },
    { key: 'mesajlasma', x: 2.05, y: 1.25 },
    { key: 'sosyal', x: 3.7, y: 1.25 },
    { key: 'randevu', x: 5.35, y: 1.25 },
    { key: 'hizmetler', x: 7.0, y: 1.25 },
  ];
  for (const g of gallery) {
    sGallery.addImage({
      path: requireImage(IMAGES[g.key]),
      x: g.x,
      y: g.y,
      w: 1.55,
      h: 3.8,
      sizing: { type: 'contain', w: 1.55, h: 3.8 },
    });
  }

  const out = path.join(OUT_DIR, 'Gumus-Makas-Platform-Ozellikleri.pptx');
  await pptx.writeFile({ fileName: out });
  return out;
}

function sectionHeading(text) {
  return new Paragraph({
    heading: HeadingLevel.HEADING_1,
    spacing: { before: 320, after: 160 },
    children: [new TextRun({ text, color: NAVY, bold: true })],
  });
}

function bodyText(text, after = 160) {
  return new Paragraph({
    spacing: { after },
    children: [new TextRun({ text, size: 24, color: DARK })],
  });
}

function featureBlock(item, imgKey) {
  const blocks = [
    new Paragraph({
      spacing: { before: 240, after: 80 },
      children: [new TextRun({ text: item.title, bold: true, size: 28, color: NAVY })],
    }),
    bodyText(item.text, imgKey ? 200 : 120),
  ];
  if (imgKey && IMAGES[imgKey] && fs.existsSync(IMAGES[imgKey])) {
    blocks.push(
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 120, after: 240 },
        children: [
          new ImageRun({
            data: fs.readFileSync(IMAGES[imgKey]),
            transformation: { width: 220, height: 420 },
            type: 'png',
          }),
        ],
      }),
    );
  }
  return blocks;
}

async function buildDocx() {
  const kapakData = fs.readFileSync(requireImage(IMAGES.kapak));

  const children = [
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 500, after: 200 },
      children: [
        new ImageRun({
          data: kapakData,
          transformation: { width: 520, height: 320 },
          type: 'png',
        }),
      ],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { after: 120 },
      children: [new TextRun({ text: 'GÜMÜŞ MAKAS', bold: true, size: 48, color: NAVY })],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { after: 80 },
      children: [
        new TextRun({ text: 'Platform Özellikleri & Vizyon', size: 28, color: GRAY, italics: true }),
      ],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { after: 400 },
      children: [
        new TextRun({
          text: 'Sahibi: Yavuzan Teknoloji ve Ticaret Ltd. Şti.',
          size: 22,
          color: DARK,
        }),
      ],
    }),
    new Paragraph({ children: [new PageBreak()] }),

    sectionHeading('Gelişmiş Platform Özellikleri'),
    ...PLATFORM_FEATURES.flatMap((f) => featureBlock(f, f.image)),

    sectionHeading('İşletme Profili & Hizmet Yönetimi'),
    bodyText(
      'İşletmeler hizmet ve paketlerini uygulama üzerinden tanımlar; müşteriler işletme profilinden tüm hizmetleri görüntüleyerek kolayca randevu alabilir.',
    ),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 200, after: 300 },
      children: [
        new ImageRun({
          data: fs.readFileSync(requireImage(IMAGES.hizmetler)),
          transformation: { width: 220, height: 420 },
          type: 'png',
        }),
      ],
    }),

    new Paragraph({ children: [new PageBreak()] }),
    sectionHeading('Risk Yönetimi & Gelecek Planları'),
    ...FUTURE_ITEMS.flatMap((item, i) => [
      new Paragraph({
        spacing: { before: i === 0 ? 0 : 200, after: 80 },
        children: [
          new TextRun({ text: `${i + 1}. ${item.title}`, bold: true, size: 26, color: NAVY }),
        ],
      }),
      bodyText(item.text),
    ]),

    sectionHeading('Vizyonumuz'),
    bodyText(VISION, 300),
  ];

  const doc = new Document({
    creator: 'Yavuzan Teknoloji ve Ticaret Ltd. Şti.',
    title: 'Gümüş Makas Platform Özellikleri',
    sections: [{ children }],
  });

  const out = path.join(OUT_DIR, 'Gumus-Makas-Platform-Ozellikleri.docx');
  fs.writeFileSync(out, await Packer.toBuffer(doc));
  return out;
}

const pptxOut = await buildPptx();
const docxOut = await buildDocx();
console.log('Oluşturuldu:');
console.log(pptxOut);
console.log(docxOut);
