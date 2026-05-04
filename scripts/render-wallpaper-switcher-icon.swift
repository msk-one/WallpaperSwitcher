import AppKit

let outputPath = CommandLine.arguments[1]
let size = 1024
let scale = CGFloat(size)
let image = NSImage(size: NSSize(width: size, height: size))

func color(_ hex: UInt32) -> NSColor {
    let red = CGFloat((hex >> 16) & 0xff) / 255.0
    let green = CGFloat((hex >> 8) & 0xff) / 255.0
    let blue = CGFloat(hex & 0xff) / 255.0
    return NSColor(calibratedRed: red, green: green, blue: blue, alpha: 1.0)
}

func rect(_ x: CGFloat, _ y: CGFloat, _ width: CGFloat, _ height: CGFloat) -> NSRect {
    NSRect(x: x * scale, y: y * scale, width: width * scale, height: height * scale)
}

image.lockFocus()
NSGraphicsContext.current?.imageInterpolation = .high

let bounds = NSRect(x: 0, y: 0, width: size, height: size)
let outer = NSBezierPath(roundedRect: bounds, xRadius: 210, yRadius: 210)
outer.addClip()

NSGradient(colors: [color(0x7EC8FF), color(0x2F95E5), color(0x0054E3)])?
    .draw(in: bounds, angle: -52)

NSGradient(colors: [color(0xC9E66D), color(0x62B735), color(0x1D7B2D)])?
    .draw(in: rect(0.0, 0.0, 1.0, 0.42), angle: -12)

color(0x7FD143).withAlphaComponent(0.76).setFill()
let hill = NSBezierPath()
hill.move(to: NSPoint(x: 0, y: 0.16 * scale))
hill.curve(
    to: NSPoint(x: 0.52 * scale, y: 0.28 * scale),
    controlPoint1: NSPoint(x: 0.15 * scale, y: 0.30 * scale),
    controlPoint2: NSPoint(x: 0.34 * scale, y: 0.34 * scale))
hill.curve(
    to: NSPoint(x: scale, y: 0.34 * scale),
    controlPoint1: NSPoint(x: 0.74 * scale, y: 0.20 * scale),
    controlPoint2: NSPoint(x: 0.83 * scale, y: 0.38 * scale))
hill.line(to: NSPoint(x: scale, y: 0))
hill.line(to: NSPoint(x: 0, y: 0))
hill.close()
hill.fill()

let lunaRect = rect(0.156, 0.205, 0.688, 0.605)
let luna = NSBezierPath(roundedRect: lunaRect, xRadius: 138, yRadius: 138)
NSGradient(colors: [color(0x1C6CFF), color(0x0054E3), color(0x003399)])?
    .draw(in: luna, angle: -47)

let paragraph = NSMutableParagraphStyle()
paragraph.alignment = .center
let attrs: [NSAttributedString.Key: Any] = [
    .font: NSFont.systemFont(ofSize: 300, weight: .heavy),
    .foregroundColor: NSColor.white,
    .paragraphStyle: paragraph,
    .kern: -8
]
("WS" as NSString).draw(in: rect(0.0, 0.36, 1.0, 0.35), withAttributes: attrs)

image.unlockFocus()

guard
    let tiff = image.tiffRepresentation,
    let bitmap = NSBitmapImageRep(data: tiff),
    let png = bitmap.representation(using: .png, properties: [:])
else {
    fatalError("Unable to render PNG")
}

try png.write(to: URL(fileURLWithPath: outputPath), options: .atomic)
