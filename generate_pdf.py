import os
import sys
import site

# Ensure user site packages are in path
user_site = site.getusersitepackages()
if user_site not in sys.path:
    sys.path.append(user_site)

# Ensure reportlab is installed
try:
    import reportlab
except ImportError:
    import subprocess
    print("reportlab not found. Installing...")
    subprocess.check_call([sys.executable, "-m", "pip", "install", "reportlab", "--user"])
    # Re-import site to get fresh paths
    import importlib
    importlib.invalidate_caches()
    if user_site not in sys.path:
        sys.path.append(user_site)

from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch

def build_pdf():
    pdf_filename = "c:\\Users\\rasto\\.gemini\\antigravity-ide\\scratch\\RetellIntegrationApi\\Quotation_Engine_Test_Report.pdf"
    
    # 0.75 in margins (54 points)
    doc = SimpleDocTemplate(
        pdf_filename, 
        pagesize=letter, 
        rightMargin=54, 
        leftMargin=54, 
        topMargin=54, 
        bottomMargin=54
    )
    
    styles = getSampleStyleSheet()
    
    # Custom Styles
    primary_color = colors.HexColor("#E65F00") # Orange Events Primary
    secondary_color = colors.HexColor("#1A365D") # Professional Deep Navy
    text_color = colors.HexColor("#2D3748") # Dark Grey Body Text
    
    title_style = ParagraphStyle(
        'DocTitle',
        parent=styles['Heading1'],
        fontName='Helvetica-Bold',
        fontSize=24,
        leading=28,
        textColor=primary_color,
        spaceAfter=15
    )
    
    subtitle_style = ParagraphStyle(
        'DocSubtitle',
        parent=styles['Normal'],
        fontName='Helvetica-Oblique',
        fontSize=11,
        leading=14,
        textColor=colors.HexColor("#718096"),
        spaceAfter=25
    )
    
    h2_style = ParagraphStyle(
        'SectionHeader',
        parent=styles['Heading2'],
        fontName='Helvetica-Bold',
        fontSize=15,
        leading=18,
        textColor=secondary_color,
        spaceBefore=15,
        spaceAfter=10,
        keepWithNext=True
    )
    
    body_style = ParagraphStyle(
        'BodyTextCustom',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=10,
        leading=14,
        textColor=text_color,
        spaceAfter=8
    )
    
    table_header_style = ParagraphStyle(
        'TableHeader',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=8,
        leading=10,
        textColor=colors.white
    )
    
    table_cell_style = ParagraphStyle(
        'TableCell',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=7.5,
        leading=9.5,
        textColor=text_color
    )
    
    table_cell_bold_style = ParagraphStyle(
        'TableCellBold',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=7.5,
        leading=9.5,
        textColor=text_color
    )

    table_cell_passed_style = ParagraphStyle(
        'TableCellPassed',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=7.5,
        leading=9.5,
        textColor=colors.HexColor("#2F855A") # Green
    )

    story = []
    
    # Title & Metadata
    story.append(Paragraph("Quotation Engine - Test Execution Report", title_style))
    story.append(Paragraph("Status: <b>All 10 Test Cases Verified & Passed</b> &nbsp;|&nbsp; Date: June 3, 2026", subtitle_style))
    
    story.append(Paragraph("1. Google Sheets Reference Data", h2_style))
    story.append(Paragraph("The calculation engine was tested against the following datasets seeded in the Google Sheets database:", body_style))
    story.append(Spacer(1, 10))
    
    # 1.1 Pricing Matrix Table
    story.append(Paragraph("<b>Table A: Pricing Matrix (Reference Rates)</b>", body_style))
    pm_data = [
        [Paragraph("Event Type", table_header_style), Paragraph("Location", table_header_style), Paragraph("Service", table_header_style), Paragraph("Pricing Type", table_header_style), Paragraph("Unit Cost", table_header_style), Paragraph("Add. Charges", table_header_style)],
        [Paragraph("Wedding", table_cell_style), Paragraph("Delhi", table_cell_style), Paragraph("Catering", table_cell_style), Paragraph("PerGuest", table_cell_style), Paragraph("1200", table_cell_style), Paragraph("0", table_cell_style)],
        [Paragraph("Wedding", table_cell_style), Paragraph("Noida", table_cell_style), Paragraph("Catering", table_cell_style), Paragraph("PerGuest", table_cell_style), Paragraph("1000", table_cell_style), Paragraph("0", table_cell_style)],
        [Paragraph("Wedding", table_cell_style), Paragraph("Delhi", table_cell_style), Paragraph("Decoration", table_cell_style), Paragraph("FlatRate", table_cell_style), Paragraph("75000", table_cell_style), Paragraph("0", table_cell_style)],
        [Paragraph("Corporate", table_cell_style), Paragraph("Delhi", table_cell_style), Paragraph("Catering", table_cell_style), Paragraph("PerGuest", table_cell_style), Paragraph("800", table_cell_style), Paragraph("0", table_cell_style)],
        [Paragraph("Birthday", table_cell_style), Paragraph("Noida", table_cell_style), Paragraph("Decoration", table_cell_style), Paragraph("FlatRate", table_cell_style), Paragraph("25000", table_cell_style), Paragraph("0", table_cell_style)],
        [Paragraph("-", table_cell_style), Paragraph("-", table_cell_style), Paragraph("DJ", table_cell_style), Paragraph("FlatRate", table_cell_style), Paragraph("15000", table_cell_style), Paragraph("0", table_cell_style)],
        [Paragraph("-", table_cell_style), Paragraph("-", table_cell_style), Paragraph("Photography", table_cell_style), Paragraph("FlatRate", table_cell_style), Paragraph("30000", table_cell_style), Paragraph("0", table_cell_style)],
        [Paragraph("Wedding", table_cell_style), Paragraph("Delhi", table_cell_style), Paragraph("AVSetup", table_cell_style), Paragraph("FlatRate", table_cell_style), Paragraph("20000", table_cell_style), Paragraph("5000", table_cell_style)],
        [Paragraph("Corporate", table_cell_style), Paragraph("-", table_cell_style), Paragraph("AVSetup", table_cell_style), Paragraph("FlatRate", table_cell_style), Paragraph("15000", table_cell_style), Paragraph("0", table_cell_style)],
    ]
    
    # 504 pt available width (612 page size - 108 margins)
    pm_table = Table(pm_data, colWidths=[90, 80, 90, 80, 84, 80])
    pm_table.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), secondary_color),
        ('ALIGN', (0,0), (-1,-1), 'LEFT'),
        ('BOTTOMPADDING', (0,0), (-1,0), 6),
        ('TOPPADDING', (0,0), (-1,0), 6),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.HexColor("#F7FAFC"), colors.white]),
        ('GRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('TOPPADDING', (0,1), (-1,-1), 4),
        ('BOTTOMPADDING', (0,1), (-1,-1), 4),
    ]))
    story.append(pm_table)
    story.append(Spacer(1, 15))
    
    # 1.2 Past Quotations Table
    story.append(Paragraph("<b>Table B: Past Quotations (Historical Data)</b>", body_style))
    pq_data = [
        [Paragraph("Event Type", table_header_style), Paragraph("Location", table_header_style), Paragraph("Guest Count", table_header_style), Paragraph("Services Included", table_header_style), Paragraph("Final Quote", table_header_style)],
        [Paragraph("Wedding", table_cell_style), Paragraph("Delhi", table_cell_style), Paragraph("200", table_cell_style), Paragraph("Catering,Decoration", table_cell_style), Paragraph("450000", table_cell_style)],
        [Paragraph("Wedding", table_cell_style), Paragraph("Noida", table_cell_style), Paragraph("250", table_cell_style), Paragraph("Catering,Decoration,DJ", table_cell_style), Paragraph("520000", table_cell_style)],
        [Paragraph("Wedding", table_cell_style), Paragraph("Delhi", table_cell_style), Paragraph("350", table_cell_style), Paragraph("Catering,Decoration,DJ,Photography", table_cell_style), Paragraph("620000", table_cell_style)],
        [Paragraph("Corporate", table_cell_style), Paragraph("Delhi", table_cell_style), Paragraph("150", table_cell_style), Paragraph("Catering,AVSetup", table_cell_style), Paragraph("150000", table_cell_style)],
        [Paragraph("Corporate", table_cell_style), Paragraph("Noida", table_cell_style), Paragraph("120", table_cell_style), Paragraph("Catering,DJ", table_cell_style), Paragraph("130000", table_cell_style)],
        [Paragraph("Birthday", table_cell_style), Paragraph("Noida", table_cell_style), Paragraph("80", table_cell_style), Paragraph("Decoration,DJ", table_cell_style), Paragraph("45000", table_cell_style)],
    ]
    pq_table = Table(pq_data, colWidths=[100, 90, 80, 144, 90])
    pq_table.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), secondary_color),
        ('ALIGN', (0,0), (-1,-1), 'LEFT'),
        ('BOTTOMPADDING', (0,0), (-1,0), 6),
        ('TOPPADDING', (0,0), (-1,0), 6),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.HexColor("#F7FAFC"), colors.white]),
        ('GRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('TOPPADDING', (0,1), (-1,-1), 4),
        ('BOTTOMPADDING', (0,1), (-1,-1), 4),
    ]))
    story.append(pq_table)
    
    # Page Break for the main execution matrix
    story.append(PageBreak())
    
    # 2. Execution Matrix Section
    story.append(Paragraph("2. Test Execution Matrix (Verified via Swagger)", h2_style))
    story.append(Paragraph("The following table details the 10 distinct logical test cases executed. All results matched business requirements precisely:", body_style))
    story.append(Spacer(1, 10))
    
    headers = [
        Paragraph("ID", table_header_style), 
        Paragraph("Test Scenario", table_header_style), 
        Paragraph("Input Parameters", table_header_style), 
        Paragraph("Expected Calculation Logic", table_header_style), 
        Paragraph("Verified Swagger Output Range", table_header_style), 
        Paragraph("Status", table_header_style)
    ]
    
    matrix_rows = [
        headers,
        [
            Paragraph("<b>1</b>", table_cell_bold_style),
            Paragraph("Priority 1 Match", table_cell_style),
            Paragraph("Wedding, Delhi, 200 Guests<br/>Reqs: Catering, Decor", table_cell_style),
            Paragraph("Blends standard Catering & Decor (₹3,15,000) with matching Delhi history (₹4,50,000)", table_cell_style),
            Paragraph("<b>₹3,44,250.00 to ₹4,20,750.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>2</b>", table_cell_bold_style),
            Paragraph("Noida Rates", table_cell_style),
            Paragraph("Wedding, Noida, 250 Guests<br/>Reqs: Catering, DJ", table_cell_style),
            Paragraph("Noida Catering (₹2,50,000) + DJ (₹15,000) blended with Noida history (₹5,20,000)", table_cell_style),
            Paragraph("<b>₹3,53,250.00 to ₹4,31,750.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>3</b>", table_cell_bold_style),
            Paragraph("Priority 2 Match", table_cell_style),
            Paragraph("Corporate, Noida, 100 Guests<br/>Reqs: AVSetup", table_cell_style),
            Paragraph("Corporate AV (₹15,000) blended with Noida history (₹1,30,000)", table_cell_style),
            Paragraph("<b>₹65,250.00 to ₹79,750.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>4</b>", table_cell_bold_style),
            Paragraph("Priority 3 Match", table_cell_style),
            Paragraph("Birthday, Delhi, 100 Guests<br/>Reqs: Photography", table_cell_style),
            Paragraph("Photography flat rate (₹30,000) blended with history average (₹45,000)", table_cell_style),
            Paragraph("<b>₹33,750.00 to ₹41,250.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>5</b>", table_cell_bold_style),
            Paragraph("Unrecognized Reqs", table_cell_style),
            Paragraph("Wedding, Delhi, 100 Guests<br/>Reqs: Live Band, Valet", table_cell_style),
            Paragraph("Fallback package: (100 * 1500) + 30000 = ₹1,80,000. No history matches.", table_cell_style),
            Paragraph("<b>₹1,62,000.00 to ₹1,98,000.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>6</b>", table_cell_bold_style),
            Paragraph("Default Guests", table_cell_style),
            Paragraph("Wedding, Delhi, -10 Guests<br/>Reqs: Photography", table_cell_style),
            Paragraph("Guests defaulted to 100. Photography flat rate (₹30,000). No history matches.", table_cell_style),
            Paragraph("<b>₹27,000.00 to ₹33,000.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>7</b>", table_cell_bold_style),
            Paragraph("Mixed Pricing", table_cell_style),
            Paragraph("Wedding, Delhi, 150 Guests<br/>Reqs: Catering, Decor, DJ", table_cell_style),
            Paragraph("PerGuest Catering (₹1,80,000) + Decor (₹75,000) + DJ (₹15,000). No history.", table_cell_style),
            Paragraph("<b>₹2,43,000.00 to ₹2,97,000.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>8</b>", table_cell_bold_style),
            Paragraph("Delhi Priority", table_cell_style),
            Paragraph("Wedding, Delhi, 300 Guests<br/>Reqs: Catering", table_cell_style),
            Paragraph("Delhi Catering (₹3,60,000) blended with Delhi history (₹6,20,000). Noida ignored.", table_cell_style),
            Paragraph("<b>₹4,41,000.00 to ₹5,39,000.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>9</b>", table_cell_bold_style),
            Paragraph("Prioritized Fallback", table_cell_style),
            Paragraph("Wedding, Delhi, 250 Guests<br/>Reqs: Catering", table_cell_style),
            Paragraph("Delhi Catering (₹3,00,000) blended with Delhi history (₹4,50,000). Noida ignored.", table_cell_style),
            Paragraph("<b>₹3,37,500.00 to ₹4,12,500.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ],
        [
            Paragraph("<b>10</b>", table_cell_bold_style),
            Paragraph("Matrix Only", table_cell_style),
            Paragraph("Wedding, Delhi, 500 Guests<br/>Reqs: Catering, Decor", table_cell_style),
            Paragraph("Catering + Decor (₹6,75,000). No history matches within ±20% guest count.", table_cell_style),
            Paragraph("<b>₹6,07,500.00 to ₹7,42,500.00</b>", table_cell_bold_style),
            Paragraph("PASSED", table_cell_passed_style)
        ]
    ]
    
    # Available width: 504 pt
    matrix_table = Table(matrix_rows, colWidths=[20, 80, 110, 160, 94, 40])
    matrix_table.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), secondary_color),
        ('ALIGN', (0,0), (-1,-1), 'LEFT'),
        ('BOTTOMPADDING', (0,0), (-1,0), 6),
        ('TOPPADDING', (0,0), (-1,0), 6),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.HexColor("#F7FAFC"), colors.white]),
        ('GRID', (0,0), (-1,-1), 0.5, colors.HexColor("#E2E8F0")),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('TOPPADDING', (0,1), (-1,-1), 5),
        ('BOTTOMPADDING', (0,1), (-1,-1), 5),
    ]))
    
    story.append(matrix_table)
    
    # Build Document
    doc.build(story)
    print("PDF Generation complete.")

if __name__ == "__main__":
    build_pdf()
