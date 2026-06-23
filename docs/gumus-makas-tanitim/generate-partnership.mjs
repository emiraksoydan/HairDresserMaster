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

const KAPAK = path.join(__dirname, 'kapak.png');

function requireImage(p) {
  if (!fs.existsSync(p)) throw new Error(`Görsel bulunamadı: ${p}`);
  return p;
}

const INTRO =
  'Gümüş Makas\'ın sürdürülebilir büyümesini hızlandırmak ve sektörde güçlü bir ekosistem oluşturmak amacıyla aşağıdaki iş birliği modelleri değerlendirilmektedir.';

const PARTNERSHIPS = [
  {
    title: 'Stratejik İş Ortaklığı',
    text: 'Sektör temsilcileri, kurumlar ve teknoloji firmaları ile uzun vadeli iş birlikleri kurularak platformun kullanıcı ağı ve hizmet kapasitesinin büyütülmesi.',
  },
  {
    title: 'Kurumsal Doğrulama Entegrasyonu',
    text: 'Kullanıcı güvenini artırmak amacıyla işletmelerin vergi levhası, mesleki yeterlilik ve resmi belgelerinin ücretsiz olarak doğrulanmasını sağlayacak entegrasyonların hayata geçirilmesi.',
  },
  {
    title: 'Paydaşlık Modeli',
    text: 'Meslek odaları, sektör birlikleri, eğitim kurumları ve ticari kuruluşlarla ortak projeler geliştirilerek platformun sektörel dönüşümüne katkı sağlanması.',
  },
  {
    title: 'Yatırım ve Büyüme Ortaklığı',
    text: 'Stratejik yatırımcılar ve kurumsal iş ortakları ile birlikte platformun teknolojik altyapısının güçlendirilmesi, yeni hizmet alanlarının geliştirilmesi ve ulusal/uluslararası ölçekte büyüme hedeflerinin desteklenmesi.',
  },
];

const CONTACT = {
  provider: 'Yavuzan Teknoloji ve Ticaret LTD.ŞTİ',
  address: 'Sakız Ağacı Mahallesi, İstanbul Caddesi, 78/A Bakırköy/İstanbul',
  name: 'Safter Yavuz',
  phone: '0546 478 27 44',
  email: 'yavuzanteknoloji@gmail.com',
};

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

async function buildPptx() {
  const pptx = new PptxGenJS();
  pptx.author = 'Yavuzan Teknoloji ve Ticaret Ltd. Şti.';
  pptx.title = 'Gümüş Makas İş Birliği ve Stratejik Ortaklık';
  pptx.layout = 'LAYOUT_16x9';

  // Kapak
  const s1 = pptx.addSlide();
  s1.background = { color: 'F5F5F5' };
  s1.addImage({
    path: requireImage(KAPAK),
    x: 0,
    y: 0,
    w: '100%',
    h: '100%',
    sizing: { type: 'cover', w: '100%', h: '100%' },
  });
  s1.addShape(pptx.ShapeType.rect, {
    x: 0,
    y: 4.55,
    w: '100%',
    h: 1.05,
    fill: { color: NAVY, transparency: 12 },
  });
  s1.addText('İş Birliği ve Stratejik Ortaklık', {
    x: 0.5,
    y: 4.7,
    w: 9,
    h: 0.45,
    fontSize: 28,
    bold: true,
    color: 'FFFFFF',
    align: 'center',
  });
  s1.addText('Seçenekleri', {
    x: 0.5,
    y: 5.15,
    w: 9,
    h: 0.35,
    fontSize: 20,
    color: GOLD,
    align: 'center',
  });

  // Giriş
  const s2 = pptx.addSlide();
  addHeaderBar(s2, pptx, 'İş Birliği ve Stratejik Ortaklık Seçenekleri', CONTACT.provider);
  s2.addText(INTRO, {
    x: 0.55,
    y: 1.4,
    w: 8.9,
    h: 1.4,
    fontSize: 16,
    color: DARK,
    lineSpacing: 26,
  });
  s2.addShape(pptx.ShapeType.rect, {
    x: 0.55,
    y: 3.0,
    w: 8.9,
    h: 0.04,
    fill: { color: GOLD },
  });
  s2.addText(
    PARTNERSHIPS.map((p) => ({ text: p.title, options: { bullet: true } })),
    {
      x: 0.65,
      y: 3.25,
      w: 8.6,
      h: 2.5,
      fontSize: 16,
      color: NAVY,
      bold: true,
      lineSpacing: 28,
    },
  );

  // Ortaklık modelleri 1-2
  const s3 = pptx.addSlide();
  addHeaderBar(s3, pptx, 'Ortaklık Modelleri', '1 – 2');
  let y = 1.25;
  PARTNERSHIPS.slice(0, 2).forEach((item, i) => {
    s3.addShape(pptx.ShapeType.ellipse, {
      x: 0.55,
      y: y + 0.05,
      w: 0.45,
      h: 0.45,
      fill: { color: GOLD },
    });
    s3.addText(String(i + 1), {
      x: 0.55,
      y: y + 0.05,
      w: 0.45,
      h: 0.45,
      fontSize: 16,
      bold: true,
      color: NAVY,
      align: 'center',
      valign: 'mid',
    });
    s3.addText(item.title, {
      x: 1.15,
      y,
      w: 8.2,
      h: 0.4,
      fontSize: 18,
      bold: true,
      color: NAVY,
    });
    s3.addText(item.text, {
      x: 1.15,
      y: y + 0.45,
      w: 8.2,
      h: 1.1,
      fontSize: 14,
      color: DARK,
      lineSpacing: 22,
    });
    y += 1.85;
  });

  // Ortaklık modelleri 3-4
  const s4 = pptx.addSlide();
  addHeaderBar(s4, pptx, 'Ortaklık Modelleri', '3 – 4');
  y = 1.25;
  PARTNERSHIPS.slice(2).forEach((item, idx) => {
    const n = idx + 3;
    s4.addShape(pptx.ShapeType.ellipse, {
      x: 0.55,
      y: y + 0.05,
      w: 0.45,
      h: 0.45,
      fill: { color: GOLD },
    });
    s4.addText(String(n), {
      x: 0.55,
      y: y + 0.05,
      w: 0.45,
      h: 0.45,
      fontSize: 16,
      bold: true,
      color: NAVY,
      align: 'center',
      valign: 'mid',
    });
    s4.addText(item.title, {
      x: 1.15,
      y,
      w: 8.2,
      h: 0.4,
      fontSize: 18,
      bold: true,
      color: NAVY,
    });
    s4.addText(item.text, {
      x: 1.15,
      y: y + 0.45,
      w: 8.2,
      h: 1.1,
      fontSize: 14,
      color: DARK,
      lineSpacing: 22,
    });
    y += 1.85;
  });

  // Özet
  const s5 = pptx.addSlide();
  s5.background = { color: NAVY };
  s5.addText('Birlikte Büyüyelim', {
    x: 0.5,
    y: 1.0,
    w: 9,
    h: 0.6,
    fontSize: 32,
    bold: true,
    color: GOLD,
    align: 'center',
  });
  s5.addText(
    'Gümüş Makas; randevu, iletişim, iş yönetimi, alışveriş ve dijital dönüşümü tek platformda birleştirerek sektörde güçlü ve sürdürülebilir bir ekosistem oluşturmayı hedeflemektedir. Stratejik ortaklıklarınızla bu vizyonu birlikte hayata geçirebiliriz.',
    {
      x: 0.75,
      y: 1.85,
      w: 8.5,
      h: 2.2,
      fontSize: 16,
      color: 'FFFFFF',
      lineSpacing: 26,
      align: 'center',
    },
  );
  s5.addShape(pptx.ShapeType.rect, {
    x: 2.5,
    y: 4.2,
    w: 5,
    h: 0.04,
    fill: { color: GOLD },
  });
  s5.addText('İş birliği teklifleriniz için bizimle iletişime geçin.', {
    x: 0.5,
    y: 4.45,
    w: 9,
    h: 0.4,
    fontSize: 15,
    color: 'CCCCCC',
    align: 'center',
    italic: true,
  });

  // İletişim
  const s6 = pptx.addSlide();
  addHeaderBar(s6, pptx, 'İletişim', 'Yavuzan Teknoloji ve Ticaret LTD.ŞTİ');
  const rows = [
    { label: 'Sağlayıcı', value: CONTACT.provider },
    { label: 'Adres', value: CONTACT.address },
    { label: 'İsim', value: CONTACT.name },
    { label: 'Telefon', value: CONTACT.phone },
    { label: 'E-posta', value: CONTACT.email },
  ];
  y = 1.45;
  rows.forEach((row) => {
    s6.addText(row.label, {
      x: 0.75,
      y,
      w: 2.0,
      h: 0.4,
      fontSize: 14,
      bold: true,
      color: NAVY,
    });
    s6.addText(row.value, {
      x: 2.85,
      y,
      w: 6.4,
      h: 0.55,
      fontSize: 14,
      color: DARK,
    });
    y += 0.75;
  });
  s6.addImage({
    path: requireImage(KAPAK),
    x: 7.2,
    y: 4.0,
    w: 2.3,
    h: 1.4,
    sizing: { type: 'contain', w: 2.3, h: 1.4 },
  });

  const out = path.join(OUT_DIR, 'Gumus-Makas-Is-Birligi.pptx');
  await pptx.writeFile({ fileName: out });
  return out;
}

function sectionHeading(text, before = 320) {
  return new Paragraph({
    heading: HeadingLevel.HEADING_1,
    spacing: { before, after: 160 },
    children: [new TextRun({ text, color: NAVY, bold: true })],
  });
}

function bodyText(text, after = 160) {
  return new Paragraph({
    spacing: { after },
    children: [new TextRun({ text, size: 24, color: DARK })],
  });
}

async function buildDocx() {
  const kapakData = fs.readFileSync(requireImage(KAPAK));

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
        new TextRun({
          text: 'İş Birliği ve Stratejik Ortaklık Seçenekleri',
          size: 26,
          color: GRAY,
          italics: true,
        }),
      ],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { after: 400 },
      children: [
        new TextRun({ text: CONTACT.provider, size: 22, color: DARK }),
      ],
    }),
    new Paragraph({ children: [new PageBreak()] }),

    sectionHeading('İş Birliği ve Stratejik Ortaklık Seçenekleri', 0),
    bodyText(INTRO, 280),
    ...PARTNERSHIPS.flatMap((item, i) => [
      new Paragraph({
        spacing: { before: i === 0 ? 0 : 240, after: 80 },
        children: [
          new TextRun({ text: `${i + 1}. ${item.title}`, bold: true, size: 28, color: NAVY }),
        ],
      }),
      bodyText(item.text, 200),
    ]),

    sectionHeading('Birlikte Büyüyelim'),
    bodyText(
      'Gümüş Makas; randevu, iletişim, iş yönetimi, alışveriş ve dijital dönüşümü tek platformda birleştirerek sektörde güçlü ve sürdürülebilir bir ekosistem oluşturmayı hedeflemektedir. Stratejik ortaklıklarınızla bu vizyonu birlikte hayata geçirebiliriz.',
      300,
    ),

    new Paragraph({ children: [new PageBreak()] }),
    sectionHeading('İletişim', 0),
    bodyText(`Sağlayıcı: ${CONTACT.provider}`),
    bodyText(`Adres: ${CONTACT.address}`),
    bodyText(`İsim: ${CONTACT.name}`),
    bodyText(`Tel: ${CONTACT.phone}`),
    bodyText(`Mail: ${CONTACT.email}`, 300),
  ];

  const doc = new Document({
    creator: CONTACT.provider,
    title: 'Gümüş Makas İş Birliği ve Stratejik Ortaklık',
    sections: [{ children }],
  });

  const out = path.join(OUT_DIR, 'Gumus-Makas-Is-Birligi.docx');
  fs.writeFileSync(out, await Packer.toBuffer(doc));
  return out;
}

const pptxOut = await buildPptx();
const docxOut = await buildDocx();
console.log('Oluşturuldu:');
console.log(pptxOut);
console.log(docxOut);
