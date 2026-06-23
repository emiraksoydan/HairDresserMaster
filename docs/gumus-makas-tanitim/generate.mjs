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
  BorderStyle,
  Table,
  TableRow,
  TableCell,
  WidthType,
  ShadingType,
} from 'docx';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = __dirname;

const KAPAK = path.join(__dirname, 'kapak.png');
const APP_SCREEN = path.join(__dirname, 'uygulama-ekrani.png');

function resolveImage(p) {
  if (fs.existsSync(p)) return p;
  throw new Error(`Görsel bulunamadı: ${p}`);
}

const kapakPath = resolveImage(KAPAK);
const appPath = resolveImage(APP_SCREEN);

const NAVY = '173D62';
const GOLD = 'C9A227';
const DARK = '1A1A1A';
const GRAY = '555555';

const INTRO = `Gümüş Makas, müşterileri, işletmeleri ve serbest çalışan profesyonelleri tek platformda buluşturan yeni nesil randevu ve iş yönetimi uygulamasıdır.

Sadece randevu oluşturmanın ötesinde; işletmelerin boş kapasitesini değerlendirmesine, boş koltuklarını hem serbest çalışanlara hem de müşterilere ulaştırmasına, serbest çalışan profesyonellerin yeni müşterilere ve uygun işletmelere kolayca erişmesine, kullanıcıların ise istedikleri yerde, istedikleri profesyonelden güvenli ve hızlı şekilde hizmet almasına olanak sağlayan kapsamlı bir dijital ekosistem sunar.`;

const FEATURES = [
  'Online randevu sistemi',
  'İşletme, serbest çalışan ve kullanıcı profilleri',
  'Boş koltuk ve çalışma alanı paylaşımı',
  'Güvenli ve doğrulanmış işletme yapısı',
  'Kampanya ve reklam yönetimi',
  'Konum bazlı hızlı keşif (haritada ara)',
  'Sektöre yön verecek özel dijital dönüşüm altyapısı',
];

const ADVANTAGES = [
  {
    title: 'Kendi Müşterisine Randevu Oluşturma',
    text: 'İşletme, uygulama üzerinden kendi müşterileri adına hızlı ve kolay şekilde randevu oluşturabilir, takvimini etkin şekilde yönetebilir.',
  },
  {
    title: 'Serbest Çalışan İçin Koltuk Kiralama',
    text: 'Belge sahibi serbest çalışan profesyoneller, işletmenin boş koltuklarını belirlenen süre veya şartlarda kiralayabilir. Böylece işletmenin atıl kapasitesi gelire dönüşür.',
  },
  {
    title: 'En Yakın Serbest Çalışanı Anlık Çağırma',
    text: 'Yoğunluk yaşandığında işletme, konumuna en yakın uygun serbest çalışanı uygulama üzerinden anlık olarak davet ederek müşterisini bekletmeden hizmet verebilir.',
  },
  {
    title: 'Güçlü Kombinasyon Sistemi',
    text: 'Serbest çalışanın kendi müşterisi ile işletmenin boş kapasitesi akıllı eşleştirme sistemi sayesinde buluşturulur. Böylece hem serbest çalışan hem işletme hem de müşteri aynı anda kazanır.',
  },
  {
    title: 'Mobil ve Esnek Çalışma İmkânı',
    text: 'İşletme sahibi veya çalışan profesyoneller, diledikleri zaman farklı işletmelerde serbest çalışan olarak hizmet verebilir ve ek gelir elde edebilir.',
  },
];

const ECOSYSTEM = [
  'Boş koltukların değerlendirilmesini',
  'Müşteri kaybının önlenmesini',
  'Serbest çalışanların daha fazla iş fırsatına ulaşmasını',
  'İşletmelerin gelirlerini artırmasını',
  'Kullanıcıların güvenilir ve hızlı hizmet almasını sağlar',
];

async function buildPptx() {
  const pptx = new PptxGenJS();
  pptx.author = 'Yavuzan Teknoloji ve Ticaret Ltd. Şti.';
  pptx.title = 'Gümüş Makas Tanıtım';
  pptx.subject = 'Randevu ve İş Yönetimi Ekosistemi';
  pptx.layout = 'LAYOUT_16x9';

  const masterOpts = { x: 0.5, y: 0.35, w: 9, h: 0.6, fontSize: 28, bold: true, color: NAVY };
  const subOpts = { x: 0.5, y: 1.0, w: 9, h: 0.4, fontSize: 14, color: GRAY };
  const bulletOpts = { x: 0.7, y: 1.6, w: 8.8, h: 4.5, fontSize: 16, color: DARK, bullet: true, lineSpacing: 28 };

  // Kapak
  const s1 = pptx.addSlide();
  s1.background = { color: 'F5F5F5' };
  s1.addImage({ path: kapakPath, x: 0, y: 0, w: '100%', h: '100%', sizing: { type: 'cover', w: '100%', h: '100%' } });

  // Tanıtım
  const s2 = pptx.addSlide();
  s2.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: '100%', h: 0.12, fill: { color: NAVY } });
  s2.addText('GÜMÜŞ MAKAS', masterOpts);
  s2.addText('Yavuzan Teknoloji ve Ticaret Ltd. Şti.', subOpts);
  s2.addText(INTRO, {
    x: 0.55,
    y: 1.55,
    w: 8.9,
    h: 4.8,
    fontSize: 15,
    color: DARK,
    lineSpacing: 24,
    valign: 'top',
  });

  // Özellikler
  const s3 = pptx.addSlide();
  s3.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: '100%', h: 0.12, fill: { color: NAVY } });
  s3.addText('Öne Çıkan Özellikler', masterOpts);
  s3.addText(FEATURES.map((t) => ({ text: t, options: { bullet: true } })), bulletOpts);

  // Avantajlar 1-3
  const s4 = pptx.addSlide();
  s4.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: '100%', h: 0.12, fill: { color: NAVY } });
  s4.addText('İşletmelere Sağladığı Avantajlar', { ...masterOpts, fontSize: 24 });
  let y = 1.2;
  ADVANTAGES.slice(0, 3).forEach((item, i) => {
    s4.addText(`${i + 1}. ${item.title}`, {
      x: 0.55,
      y,
      w: 8.8,
      h: 0.35,
      fontSize: 16,
      bold: true,
      color: NAVY,
    });
    s4.addText(item.text, {
      x: 0.75,
      y: y + 0.38,
      w: 8.5,
      h: 0.85,
      fontSize: 13,
      color: DARK,
      lineSpacing: 18,
    });
    y += 1.35;
  });

  // Avantajlar 4-5 + Ekosistem
  const s5 = pptx.addSlide();
  s5.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: '100%', h: 0.12, fill: { color: NAVY } });
  s5.addText('Avantajlar & Ekosistem', { ...masterOpts, fontSize: 24 });
  y = 1.15;
  ADVANTAGES.slice(3).forEach((item, idx) => {
    const n = idx + 4;
    s5.addText(`${n}. ${item.title}`, {
      x: 0.55,
      y,
      w: 8.8,
      h: 0.35,
      fontSize: 16,
      bold: true,
      color: NAVY,
    });
    s5.addText(item.text, {
      x: 0.75,
      y: y + 0.38,
      w: 8.5,
      h: 0.75,
      fontSize: 13,
      color: DARK,
      lineSpacing: 18,
    });
    y += 1.25;
  });
  s5.addText('Gümüş Makas Ekosistemi', {
    x: 0.55,
    y: 3.55,
    w: 8.8,
    h: 0.4,
    fontSize: 18,
    bold: true,
    color: GOLD,
  });
  s5.addText(
    'İşletme + Serbest Çalışan + Müşteri üçlüsünü tek platformda bir araya getirerek:',
    { x: 0.55, y: 3.95, w: 8.8, h: 0.45, fontSize: 13, color: DARK },
  );
  s5.addText(ECOSYSTEM.map((t) => ({ text: t, options: { bullet: true } })), { ...bulletOpts, y: 4.35, h: 1.8, fontSize: 14 });

  // Uygulama ekranı
  const s6 = pptx.addSlide();
  s6.background = { color: 'F8F8F8' };
  s6.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: '100%', h: 0.12, fill: { color: NAVY } });
  s6.addText('Mobil Uygulama', { ...masterOpts, y: 0.25, fontSize: 26 });
  s6.addText('Kullanıcı dostu arayüz ile keşif, randevu ve iş yönetimi tek ekranda', {
    x: 0.55,
    y: 0.85,
    w: 8.8,
    h: 0.4,
    fontSize: 14,
    color: GRAY,
  });
  s6.addImage({
    path: appPath,
    x: 2.8,
    y: 1.35,
    w: 4.4,
    h: 4.2,
    sizing: { type: 'contain', w: 4.4, h: 4.2 },
    shadow: { type: 'outer', blur: 8, offset: 3, angle: 45, color: '000000', opacity: 0.25 },
  });

  const out = path.join(OUT_DIR, 'Gumus-Makas-Tanitim.pptx');
  await pptx.writeFile({ fileName: out });
  return out;
}

function bulletParagraphs(items, size = 24) {
  return items.map(
    (text) =>
      new Paragraph({
        spacing: { after: 120 },
        children: [new TextRun({ text: `• ${text}`, size, color: DARK })],
      }),
  );
}

async function buildDocx() {
  const kapakData = fs.readFileSync(kapakPath);
  const appData = fs.readFileSync(appPath);

  const doc = new Document({
    creator: 'Yavuzan Teknoloji ve Ticaret Ltd. Şti.',
    title: 'Gümüş Makas Tanıtım',
    description: 'Randevu ve iş yönetimi ekosistemi tanıtım dokümanı',
    sections: [
      {
        properties: {
          page: {
            margin: { top: 720, right: 900, bottom: 720, left: 900 },
          },
        },
        children: [
          // Kapak
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 600, after: 200 },
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
            children: [
              new TextRun({ text: 'GÜMÜŞ MAKAS', bold: true, size: 48, color: NAVY }),
            ],
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { after: 80 },
            children: [
              new TextRun({
                text: 'Tanıtım Dokümanı',
                size: 28,
                color: GRAY,
                italics: true,
              }),
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

          // Giriş
          new Paragraph({
            heading: HeadingLevel.HEADING_1,
            spacing: { after: 200 },
            children: [new TextRun({ text: 'Tanıtım', color: NAVY })],
          }),
          new Paragraph({
            spacing: { after: 200 },
            children: [new TextRun({ text: INTRO, size: 24, color: DARK })],
          }),

          new Paragraph({
            heading: HeadingLevel.HEADING_1,
            spacing: { before: 240, after: 200 },
            children: [new TextRun({ text: 'Öne Çıkan Özellikler', color: NAVY })],
          }),
          ...bulletParagraphs(FEATURES),

          new Paragraph({
            heading: HeadingLevel.HEADING_1,
            spacing: { before: 360, after: 200 },
            children: [new TextRun({ text: 'İşletmelere Sağladığı Avantajlar', color: NAVY })],
          }),
          ...ADVANTAGES.flatMap((item, i) => [
            new Paragraph({
              spacing: { before: i === 0 ? 0 : 200, after: 80 },
              children: [
                new TextRun({
                  text: `${i + 1}. ${item.title}`,
                  bold: true,
                  size: 26,
                  color: NAVY,
                }),
              ],
            }),
            new Paragraph({
              spacing: { after: 120 },
              children: [new TextRun({ text: item.text, size: 24, color: DARK })],
            }),
          ]),

          new Paragraph({
            heading: HeadingLevel.HEADING_1,
            spacing: { before: 360, after: 200 },
            children: [new TextRun({ text: 'Gümüş Makas Ekosistemi', color: NAVY })],
          }),
          new Paragraph({
            spacing: { after: 160 },
            children: [
              new TextRun({
                text: 'İşletme + Serbest Çalışan + Müşteri üçlüsünü tek platformda bir araya getirerek:',
                size: 24,
                color: DARK,
              }),
            ],
          }),
          ...bulletParagraphs(ECOSYSTEM),

          // Uygulama ekranı — tüm yazılardan sonra
          new Paragraph({ children: [new PageBreak()] }),
          new Paragraph({
            heading: HeadingLevel.HEADING_1,
            alignment: AlignmentType.CENTER,
            spacing: { after: 200 },
            children: [new TextRun({ text: 'Mobil Uygulama', color: NAVY })],
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { after: 300 },
            children: [
              new ImageRun({
                data: appData,
                transformation: { width: 280, height: 520 },
                type: 'png',
              }),
            ],
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            children: [
              new TextRun({
                text: 'Gümüş Makas — Randevu, keşif ve iş yönetimi tek platformda',
                size: 22,
                color: GRAY,
                italics: true,
              }),
            ],
          }),
        ],
      },
    ],
  });

  const buffer = await Packer.toBuffer(doc);
  const out = path.join(OUT_DIR, 'Gumus-Makas-Tanitim.docx');
  fs.writeFileSync(out, buffer);
  return out;
}

const pptxOut = await buildPptx();
const docxOut = await buildDocx();
console.log('Oluşturuldu:');
console.log(pptxOut);
console.log(docxOut);
