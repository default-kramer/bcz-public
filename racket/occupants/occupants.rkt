#lang racket/gui

(module+ main
  (save-images))

(require pict)

(define size 360)
(define size/2 180)
(define size/10 36)
(define thickness 36)
(define thickness/2 18)
(define enemy-border-color (make-color 220 220 220))
(define red-brush (new brush% [style 'solid] [color "red"]))
(define black-brush (new brush% [style 'solid] [color "black"]))
(define body-color (make-parameter (make-color 255 255 255)))
(define border-color (make-parameter enemy-border-color))

(define (single-catalyst [border-color (border-color)]
                         [body-color (body-color)])
  (cc-superimpose
   (blank size)
   (disk (- size thickness) #:color body-color
         #:border-color border-color #:border-width thickness)))

(define (joined-catalyst [border-color (border-color)]
                         [body-color (body-color)])
  (define path
    (let ([path (new dc-path%)])
      ; arc from right -> top -> left
      (let ([xy thickness/2]
            [wh (- size thickness)])
        (send path arc xy xy wh wh 0 pi))
      ; lines to left corner then right corner
      (let ([y (- size thickness/2)])
        (send path line-to thickness/2 y)
        (send path line-to y y))
      ; closing adds a line from right corner to start of arc
      (send path close)
      path))
  (dc (lambda (dc dx dy)
        (define old-brush (send dc get-brush))
        (define old-pen (send dc get-pen))
        (send dc set-brush (new brush% [color body-color]))
        (send dc set-pen (new pen%
                              [cap 'butt] ; doesn't work with draw
                              [width thickness]
                              [color border-color]))
        (send dc draw-path path dx dy)
        ; Looks like [cap 'butt] doesn't work with draw-path...?
        ; Patch over the rounded corners with some small rectangles
        (let ([y (+ dy size (- thickness/2))]
              [x0 dx]
              [x1 (+ dx size)]
              [wh thickness])
          (send dc draw-line x0 y x1 y))
        (send dc set-brush old-brush)
        (send dc set-pen old-pen))
      size size))

; In certain situations, using `#:color "transparent"` does not leave
; a transparent hole. It leaves a black hole instead.
; So you can paint it red instead and use this function.
(define (red->transparent pict)
  (define pixels (pict->argb-pixels pict 'unsmoothed))
  (for ([i (in-range 0 (bytes-length pixels) 4)])
    (let ([a (bytes-ref pixels i)]
          [r (bytes-ref pixels (+ 1 i))]
          [g (bytes-ref pixels (+ 2 i))]
          [b (bytes-ref pixels (+ 3 i))])
      (when (and (= 255 r) (= 0 g) (= 0 b))
        (bytes-set! pixels i 0)
        (bytes-set! pixels (+ 1 i) 0)
        (bytes-set! pixels (+ 2 i) 0)
        (bytes-set! pixels (+ 3 i) 0))))
  (argb-pixels->pict pixels (inexact->exact (pict-width pict))))

(define (enemy [border-color (border-color)])
  (cc-superimpose
   (blank size)
   (let* ([size (- size thickness thickness thickness/2)]
          [thickness (+ thickness thickness)])
     (filled-rounded-rectangle
      size size #:color "black"
      #:draw-border? #t #:border-color border-color #:border-width thickness))))

(define (backdrop pict color)
  (cc-superimpose
   (filled-rectangle size size #:color color #:draw-border? #f)
   pict))

(backdrop (single-catalyst) "black")
(let ([j (backdrop (joined-catalyst) "black")])
  (ht-append (rotate j (/ pi 2))
             (rotate j (- (/ pi 2)))))

(blank 10)
(backdrop (enemy) "blue")

(define (checkerboard size count light dark)
  (define one-row
    (build-list count
                (lambda (i) (filled-rectangle size size
                                              #:color (if (even? i) light dark)
                                              #:draw-border? #f))))
  (define even-row (apply hc-append one-row))
  (define odd-row (apply hc-append (append (cdr one-row) (list (car one-row)))))
  (define all-rows (take (flatten (make-list count (list even-row odd-row))) count))
  (apply vc-append all-rows))

(define (heart #:brush brush #:border-color border-color
               #:outline-color [outline-color "white"])
  (define the-dc
    (dc (lambda (dc dx dy)
          (define old-brush (send dc get-brush))
          (define old-pen (send dc get-pen))
          (send dc set-brush brush)
          (send dc set-pen (new pen% [width 20] [color outline-color]))
          (define path (new dc-path%))
          (define steps '((180 100) ; starting point, will be handled specially
                          ; curve 1
                          (195 30)
                          (360 20)
                          (300 180)
                          ; curve 2
                          (295 190)
                          (290 205)
                          (260 239)
                          ; curve 3
                          ;(290 205)
                          (260 239)
                          (180 320)
                          (180 320)))
          ; Left half should be a mirror image of the right half, so flip the x-coordinate
          ; and reverse the steps so the path continues seamlessly.
          ; (Do bezier curves work the way I hope they do? To my eye, it seems so...)
          (define steps2 (map (lambda (xy) (match xy [(cons x y)
                                                      (cons (- 180 (- x 180)) y)]))
                              (reverse steps)))
          (define (go steps)
            (match steps
              [(list (list a b) (list c d) (list e f) more ...)
               (begin
                 (send path curve-to a b c d e f)
                 (go more))]
              [(list) #t]))
          ; Move to the first step, then skip it when creating the curves:
          (send path move-to (car (first steps)) (cadr (first steps)))
          (go (cdr steps))
          (go (cdr steps2))
          (send path close)
          (send dc draw-path path dx dy)
          (send dc set-brush old-brush)
          (send dc set-pen old-pen))
        size size))
  (cc-superimpose
   (or (and border-color
            (rectangle size size #:border-color border-color #:border-width thickness))
       (blank size size))
   the-dc))

(define partial-heart-brush
  (let ([stip (checkerboard size/10 10
                            (make-color 255 180 180 0.5)
                            (make-color 255 180 180 0.7))])
    (new brush% [stipple (pict->bitmap stip)])))

(define (partial-heart percent)
  (define outline (heart #:brush (new brush% [style 'transparent]) #:border-color "white"))
  (define filled (heart #:brush partial-heart-brush #:border-color "white"))
  (define clip (- size/2))
  (define (left-half)
    (inset/clip filled 0 0 clip 0))
  (define (left-quarter)
    (inset/clip filled 0 0 clip clip))
  (define (right-quarter)
    (inset/clip filled clip 0 0 clip))
  (lt-superimpose (case percent
                    [(0) (blank 0 0)]
                    [(25) (left-quarter)]
                    [(50) (left-half)]
                    [(75) (ht-append (left-half) (right-quarter))]
                    [(100) filled])
                  outline))

(define (complete-heart)
  (heart #:brush red-brush #:border-color "red"))

(define (heart-attack)
  ; Use black so I can reuse some enemy shader code (which looks for black)
  (heart #:brush black-brush #:border-color #f #:outline-color enemy-border-color))

(scale (backdrop (complete-heart) "black") 0.1)
(scale (backdrop (partial-heart 0) "black") 0.1)
(scale (backdrop (partial-heart 25) "black") 0.1)
(scale (backdrop (partial-heart 50) "black") 0.1)
(scale (backdrop (partial-heart 75) "black") 0.1)
(scale (backdrop (partial-heart 100) "black") 0.1)

(define (save-images)
  (define results '())
  (define directory (current-directory))
  (define bgcolor (make-color 0 0 0 0))
  (define (save-bitmap pict filename)
    (let* ([pict (backdrop pict bgcolor)]
           [bmp (pict->bitmap pict 'aligned)]
           [filename (format "~a~a" directory filename)]
           [quality 100])
      (set! results (cons pict (cons filename results)))
      (send bmp save-file filename 'bmp quality)))
  ; normal catalysts
  (save-bitmap (joined-catalyst) "joined.bmp")
  (save-bitmap (single-catalyst) "single.bmp")
  ; blank catalysts
  (parameterize ([border-color (make-color 255 255 255)]
                 [body-color (make-color 0 0 0 0)])
    (save-bitmap (joined-catalyst) "blank-joined.bmp")
    (save-bitmap (single-catalyst) "blank-single.bmp"))
  ; enemy
  (save-bitmap (enemy) "enemy.bmp")
  ; hearts
  (save-bitmap (complete-heart) "heart.bmp")
  (for ([i '(0 25 50 75 100)])
    (save-bitmap (partial-heart i) (format "heart~a.bmp" i)))
  (save-bitmap (heart-attack) "heart-attack.bmp")
  ; return value
  (reverse results))
